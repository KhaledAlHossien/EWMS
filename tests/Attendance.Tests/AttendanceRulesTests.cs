using Application.Features.Attendance;
using Infrastructure.Attendance;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Attendance.Tests
{
    public class AttendanceRulesTests
    {
        [Theory]
        [InlineData("1001", "1001")]
        [InlineData("  0012 ", "12")]
        [InlineData(null, null)]
        [InlineData("", null)]
        public void NormalizeNumber_keeps_digits_without_leading_zeros(string? input, string? expected) =>
            Assert.Equal(expected, AttendanceRules.NormalizeNumber(input));

        [Theory]
        [InlineData("12a")]
        [InlineData("0000")]
        [InlineData("1234567890")]
        public void NormalizeNumber_rejects_invalid_numbers(string input) =>
            Assert.Throws<InvalidOperationException>(() => AttendanceRules.NormalizeNumber(input));

        [Theory]
        [InlineData("0012", "12")]
        [InlineData("12", "12")]
        [InlineData("0", "0")]
        [InlineData("A12", "A12")]
        public void PinKey_matches_device_pins_to_stored_numbers(string pin, string expected) =>
            Assert.Equal(expected, AttendanceRules.PinKey(pin));

        [Fact]
        public void Verify_maps_codes_to_methods()
        {
            Assert.Equal("face", AttendanceRules.Verify(15).Key);
            Assert.Equal("finger", AttendanceRules.Verify(1).Key);
            Assert.Equal("card", AttendanceRules.Verify(4).Key);
            Assert.Equal("other", AttendanceRules.Verify(99).Key);
        }

        [Fact]
        public void TemplateProtector_round_trips_and_detects_a_wrong_key()
        {
            static AttendanceTemplateProtector Make(string key) => new(new AttendanceSettings(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Attendance:TemplateKey"] = key }).Build()));

            var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
            var other = Convert.ToBase64String(new byte[32]);
            var plain = System.Text.Encoding.ASCII.GetBytes("QUJDREVG");

            var stored = Make(key).Protect(plain);

            Assert.NotEqual(plain, stored[29..]);
            Assert.Equal(plain, Make(key).Unprotect(stored));
            Assert.Throws<InvalidOperationException>(() => Make(other).Unprotect(stored));
        }

        [Fact]
        public void Settings_require_a_template_key_only_when_enabled()
        {
            static AttendanceSettings Make(params (string, string?)[] values) => new(new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(v => v.Item1, v => v.Item2)).Build());

            Assert.Null(Make().Problem());
            Assert.NotNull(Make(("Attendance:Enabled", "true")).Problem());
            Assert.Null(Make(("Attendance:Enabled", "true"), ("Attendance:TemplateKey", Convert.ToBase64String(new byte[32]))).Problem());
        }
    }
}
