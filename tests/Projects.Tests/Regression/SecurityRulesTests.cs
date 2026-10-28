using Application.Common;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Projects.Tests.Regression;

/// <summary>سياسة كلمة المرور وحماية الدخول (قرار 2026-10-13) وتشفير كلمات سر الأجهزة (قرار 2026-10-05)</summary>
public class SecurityRulesTests
{
    // ───────── كلمة المرور ─────────

    [Theory]
    [InlineData("abc12345")]
    [InlineData("كلمة_سر1234")]
    [InlineData("A1b2C3d4e5")]
    public void A_password_with_a_letter_a_digit_and_eight_characters_is_accepted(string password)
    {
        Assert.Null(PasswordRules.Problem(password, "user@x.sy"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_empty_password_is_required(string? password)
    {
        Assert.Equal("كلمة المرور مطلوبة", PasswordRules.Problem(password));
    }

    [Theory]
    [InlineData("abc1234", "أحرف على الأقل")]      // سبعة
    [InlineData("12345678", "حرفاً واحداً")]       // بلا حرف
    [InlineData("abcdefgh", "رقماً واحداً")]       // بلا رقم
    public void A_weak_password_is_rejected_with_its_reason(string password, string reason)
    {
        Assert.Contains(reason, PasswordRules.Problem(password));
    }

    [Fact]
    public void The_password_cannot_be_the_email_or_its_first_part_in_any_case()
    {
        Assert.NotNull(PasswordRules.Problem("yaman123", "yaman123@x.sy"));
        Assert.NotNull(PasswordRules.Problem("YAMAN123", "yaman123@x.sy"));
        Assert.NotNull(PasswordRules.Problem("yaman123@x.sy", "yaman123@x.sy"));
        Assert.Null(PasswordRules.Problem("yaman1234", "yaman123@x.sy"));
    }

    [Fact]
    public void The_minimum_length_boundary_is_exact()
    {
        Assert.Equal(8, PasswordRules.MinLength);
        Assert.Null(PasswordRules.Problem("abcdefg1"));
        Assert.NotNull(PasswordRules.Problem("abcdef1"));
    }

    // ───────── القفل ─────────

    [Fact]
    public void The_lockout_constants_match_the_decision()
    {
        Assert.Equal(5, LoginRules.MaxFailedAttempts);
        Assert.Equal(15, LoginRules.LockoutMinutes);
        Assert.Equal(20, LoginRules.AttemptsPerDevicePerMinute);
    }

    [Theory]
    [InlineData(0.2, "دقيقة")]
    [InlineData(1.5, "دقيقتين")]
    [InlineData(5, "5 دقائق")]
    [InlineData(14.5, "15 دقيقة")]
    public void The_lock_message_shows_the_remaining_minutes_rounded_up(double minutes, string expected)
    {
        Assert.Contains(expected, LoginRules.LockedMessage(DateTime.UtcNow.AddMinutes(minutes)));
    }

    [Fact]
    public void The_lock_message_never_says_zero_minutes()
    {
        Assert.Contains("دقيقة", LoginRules.LockedMessage(DateTime.UtcNow.AddSeconds(-30)));
    }

    // ───────── تشفير كلمات سر الأجهزة ─────────

    private static DevicePasswordProtector ProtectorWith(string? key) => new(new ConfigurationBuilder()
        .AddInMemoryCollection(key == null ? [] : new Dictionary<string, string?> { ["DeviceInventory:PasswordKey"] = key })
        .Build());

    private static string NewKey() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void A_protected_password_round_trips_and_is_not_stored_in_clear()
    {
        var protector = ProtectorWith(NewKey());

        var stored = protector.Protect("P@ss-كلمة 123");

        Assert.StartsWith("enc:v1:", stored);
        Assert.DoesNotContain("P@ss", stored);
        Assert.True(protector.IsProtected(stored));
        Assert.Equal("P@ss-كلمة 123", protector.Unprotect(stored));
    }

    [Fact]
    public void Every_encryption_uses_a_fresh_nonce()
    {
        var protector = ProtectorWith(NewKey());

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void An_empty_password_stays_empty()
    {
        var protector = ProtectorWith(NewKey());

        Assert.Equal(string.Empty, protector.Protect(string.Empty));
        Assert.Equal(string.Empty, protector.Unprotect(string.Empty));
    }

    [Fact]
    public void A_legacy_plain_text_value_is_returned_as_is_until_the_startup_migration_encrypts_it()
    {
        var protector = ProtectorWith(NewKey());

        Assert.False(protector.IsProtected("plain"));
        Assert.Equal("plain", protector.Unprotect("plain"));
    }

    [Fact]
    public void A_different_key_cannot_decrypt_and_says_so_clearly()
    {
        var stored = ProtectorWith(NewKey()).Protect("secret1");

        var error = Assert.Throws<InvalidOperationException>(() => ProtectorWith(NewKey()).Unprotect(stored));
        Assert.Contains("مفتاح التشفير", error.Message);
    }

    [Fact]
    public void A_tampered_ciphertext_is_rejected_not_decrypted_to_garbage()
    {
        var protector = ProtectorWith(NewKey());
        var stored = protector.Protect("secret1");
        var bytes = Convert.FromBase64String(stored["enc:v1:".Length..]);
        bytes[^1] ^= 0xFF;

        Assert.Throws<InvalidOperationException>(() => protector.Unprotect("enc:v1:" + Convert.ToBase64String(bytes)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!!")]
    [InlineData("QUJD")]                     // base64 صالح لكنه 3 بايت لا 32
    public void A_missing_or_invalid_key_stops_encryption_instead_of_storing_clear_text(string? key)
    {
        var protector = ProtectorWith(key);

        Assert.Throws<InvalidOperationException>(() => protector.Protect("secret1"));
    }
}
