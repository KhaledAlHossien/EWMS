using System.Runtime.CompilerServices;
using System.Text.Json;
using Application.Common;

namespace Architecture.Tests;

/// <summary>
/// عقد الصلاحيات بين الباكاند والواجهة: القائمة الكاملة لأسماء الصلاحيات وسياسات «أيّ من» في ملف ذهبي (contract/permissions.json)
/// تحت إدارة المصدر. الواجهة تحتفظ بنسخة منه وتتحقق أن access.ts يطابقها (npm run contract:sync ثم الاختبار)،
/// فلا تبقى صلاحية مضافة هنا غير معرّفة هناك (زر يختفي بصمت)، ولا اسم في الواجهة بلا مقابل هنا.
/// تحديث الملف: UPDATE_API_CONTRACT=1 ثم تشغيل الاختبار، وراجع الفرق في الـ commit.
/// </summary>
public class PermissionContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Generate() => JsonSerializer.Serialize(new
    {
        permissions = AppPermissions.All.Select(p => p.Name).Order(StringComparer.Ordinal).ToArray(),
        anyOf = AppPermissions.AnyOf.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Order(StringComparer.Ordinal).ToArray())
    }, Pretty).ReplaceLineEndings("\n") + "\n";

    [Fact]
    public void The_permission_contract_matches_the_reviewed_snapshot()
    {
        var path = SourcePath();
        var actual = Generate();

        if (Environment.GetEnvironmentVariable("UPDATE_API_CONTRACT") == "1")
        {
            File.WriteAllText(path, actual);
            return;
        }

        var expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        var now = JsonSerializer.Deserialize<Snapshot>(actual, Web)!;
        var then = JsonSerializer.Deserialize<Snapshot>(expected, Web)!;
        var added = now.Permissions.Except(then.Permissions).ToList();
        var removed = then.Permissions.Except(now.Permissions).ToList();

        Assert.True(actual == expected,
            "تغيّر كتالوج الصلاحيات عن اللقطة المراجَعة (contract/permissions.json). راجع التغيير، ثم حدّث الملف بتشغيل الاختبار مع UPDATE_API_CONTRACT=1، " +
            "وانسخه إلى الواجهة (npm run contract:sync) وعدّل access.ts وشجرة صفحة الأدوار.\n" +
            $"أُضيفت:\n{Scan.Lines(added)}\nحُذفت:\n{Scan.Lines(removed)}");
    }

    private sealed record Snapshot(string[] Permissions);

    private static string SourcePath([CallerFilePath] string callerFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerFile)!, "..", "..", "contract", "permissions.json"));
}
