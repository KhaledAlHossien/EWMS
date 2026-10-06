using System.Text.RegularExpressions;

namespace Application.Common;

/// <summary>
/// قواعد عناوين الشبكة لتوثيق الأجهزة (مطابقة لـ core/utils/network.ts في الواجهة — غيّرهما معاً):
/// IPv4 بلا أصفار بادئة، قناع شبكة متصل (آحاد ثم أصفار)، بوابة داخل شبكة الجهاز، وعنوان MAC بصيغة موحّدة.
/// </summary>
public static partial class NetworkRules
{
    [GeneratedRegex(@"^(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}$")]
    private static partial Regex Ipv4Regex();

    [GeneratedRegex(@"^[0-9A-Fa-f]{12}$")]
    private static partial Regex MacHexRegex();

    public static bool IsIpv4(string? value) => !string.IsNullOrWhiteSpace(value) && Ipv4Regex().IsMatch(value.Trim());

    private static uint ToUInt(string ip) =>
        ip.Trim().Split('.').Select(byte.Parse).Aggregate(0u, (acc, b) => (acc << 8) | b);

    /// <summary>قناع صحيح: آحاد متصلة من اليسار ثم أصفار (255.255.255.0 نعم، 255.0.255.0 لا)، وليس 0.0.0.0</summary>
    public static bool IsSubnetMask(string? value)
    {
        if (!IsIpv4(value)) return false;
        var mask = ToUInt(value!);
        if (mask == 0) return false;
        var inverted = ~mask;
        return (inverted & (inverted + 1)) == 0;   // ما بعد الآحاد أصفار فقط
    }

    /// <summary>البوابة في نفس شبكة الجهاز (بحسب القناع)</summary>
    public static bool SameSubnet(string ip, string other, string mask) =>
        IsIpv4(ip) && IsIpv4(other) && IsSubnetMask(mask) && (ToUInt(ip) & ToUInt(mask)) == (ToUInt(other) & ToUInt(mask));

    /// <summary>MAC بأي فاصل (: - . أو بلا فاصل) ← AA:BB:CC:DD:EE:FF، وفارغ يبقى فارغاً</summary>
    public static string NormalizeMac(string? value)
    {
        var hex = Regex.Replace(value ?? "", @"[\s:\-.]", "");
        if (hex.Length == 0) return string.Empty;
        return MacHexRegex().IsMatch(hex)
            ? string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2).ToUpperInvariant()))
            : value!.Trim();
    }

    /// <summary>فارغ (اختياري) أو 12 خانة سداسية بأي فاصل</summary>
    public static bool IsMac(string? value)
    {
        var hex = Regex.Replace(value ?? "", @"[\s:\-.]", "");
        return hex.Length == 0 || MacHexRegex().IsMatch(hex);
    }
}
