using System.Runtime.CompilerServices;
using System.Text.Json;
using API.SystemBuild;
using Microsoft.Extensions.Configuration;

namespace Architecture.Tests;

/// <summary>
/// الأسرار خارج المستودع (ADR-0013): الإعدادات الأساسية المتتبَّعة بلا أسرار، ومفاتيح التطوير المنشورة مرفوضة في الإنتاج،
/// فلا يُنشر نظام بمفتاح يعرفه كل من يقرأ تاريخ git.
/// </summary>
public class ConfigurationSecretsTests
{
    private static JsonElement Settings(string file, [CallerFilePath] string caller = "") =>
        JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(caller)!, "..", "..", "API", file)))).RootElement;

    [Fact]
    public void The_tracked_base_settings_hold_no_secrets()
    {
        var settings = Settings("appsettings.json");

        Assert.Equal(string.Empty, settings.GetProperty("JwtSettings").GetProperty("Key").GetString());
        Assert.Equal(string.Empty, settings.GetProperty("DeviceInventory").GetProperty("PasswordKey").GetString());
        Assert.Equal(string.Empty, settings.GetProperty("Seed").GetProperty("AdminPassword").GetString());
        Assert.DoesNotContain("password=", settings.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(settings.GetProperty("DeviceInventory").GetProperty("OldPasswordKeys").EnumerateArray());
    }

    [Fact]
    public void The_development_keys_are_known_public_keys_so_production_refuses_them()
    {
        var development = Settings("appsettings.Development.json");

        Assert.True(KnownPublicKeys.IsPublic(development.GetProperty("JwtSettings").GetProperty("Key").GetString()));
        Assert.True(KnownPublicKeys.IsPublic(development.GetProperty("DeviceInventory").GetProperty("PasswordKey").GetString()));
        foreach (var old in development.GetProperty("DeviceInventory").GetProperty("OldPasswordKeys").EnumerateArray())
            Assert.True(KnownPublicKeys.IsPublic(old.GetString()), "مفتاح قديم منشور في الإعدادات يجب أن تكون بصمته في KnownPublicKeys");
        Assert.False(KnownPublicKeys.IsPublic("a-fresh-production-key-that-is-not-in-the-repo-0123456789"));
        Assert.False(KnownPublicKeys.IsPublic(null));
    }

    private static IConfiguration Config(string jwt, string device, bool allow = false) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Server=x;Database=y",
        ["JwtSettings:Key"] = jwt,
        ["DeviceInventory:PasswordKey"] = device,
        ["Security:AllowKnownDevelopmentKeys"] = allow ? "true" : null
    }).Build();

    private static readonly string DevJwt = Settings("appsettings.Development.json").GetProperty("JwtSettings").GetProperty("Key").GetString()!;
    private static readonly string DevDevice = Settings("appsettings.Development.json").GetProperty("DeviceInventory").GetProperty("PasswordKey").GetString()!;
    private static readonly string FreshDevice = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    private const string FreshJwt = "a-fresh-production-key-that-is-not-in-the-repo-0123456789";

    [Fact]
    public void Production_refuses_the_published_development_keys_and_says_which()
    {
        var error = Assert.Throws<InvalidOperationException>(() => StartupSettings.EnsureValid(Config(DevJwt, DevDevice)));

        Assert.Contains("JwtSettings__Key", error.Message);
        Assert.Contains("DeviceInventory__PasswordKey", error.Message);
        Assert.Contains("المنشور في المستودع", error.Message);
    }

    [Fact]
    public void Production_accepts_its_own_keys()
    {
        StartupSettings.EnsureValid(Config(FreshJwt, FreshDevice));
    }

    [Fact]
    public void Development_accepts_the_published_keys()
    {
        StartupSettings.EnsureValid(Config(DevJwt, DevDevice), isDevelopment: true);
    }

    [Fact]
    public void A_transitional_override_lets_production_start_with_the_published_keys()
    {
        StartupSettings.EnsureValid(Config(DevJwt, DevDevice, allow: true));
    }
}
