using System.Runtime.CompilerServices;

namespace Architecture.Tests;

/// <summary>
/// مصفوفة الصلاحيات: كل نقطة نهاية وسياستها في ملف ذهبي (permission-matrix.txt) تحت إدارة المصدر.
/// أي تغيير في صلاحية نقطة (تبديل سياسة، إضافة نقطة، حذفها) يُظهر فرقاً في الـ commit فيراجعه إنسان —
/// لا يمرّ بصمت. تحديث الملف: متغير البيئة UPDATE_PERMISSION_MATRIX=1 ثم تشغيل هذا الاختبار.
/// </summary>
public class PermissionMatrixTests
{
    private const string FileName = "permission-matrix.txt";

    /// <summary>متحكمان بوابتهما «عرض» لكن عملياتهما تغيّر بيانات صاحب الطلب نفسه، والحدّ الفعلي في المعالج (قرار موثّق في docs/architecture/quality-gates.md وdocs/architecture/task-board.md)</summary>
    private static readonly HashSet<string> ViewGatedByDesign = ["AssignedTasksController", "NotificationsController"];

    /// <summary>صلاحية قراءة بحتة: View* أو سياسة «أيّ من» للعرض</summary>
    private static bool IsViewOnly(string policy) => policy.StartsWith("View") || (policy.StartsWith("Any") && policy.EndsWith("View"));

    private static string Policy(Scan.ActionInfo a) => a.AllowsAnonymous || !a.Authenticated ? "(anonymous)" : a.Policy ?? "(authenticated only)";

    public static IReadOnlyList<string> Generate() =>
        Scan.Actions().Select(a => $"{a.Route} → {Policy(a)}").Order(StringComparer.Ordinal).ToList();

    [Fact]
    public void The_endpoint_permission_matrix_matches_the_reviewed_snapshot()
    {
        var actual = Generate();

        if (Environment.GetEnvironmentVariable("UPDATE_PERMISSION_MATRIX") == "1")
        {
            File.WriteAllLines(SourcePath(), actual);
            return;
        }

        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, FileName)).Where(l => l.Length > 0).ToList();
        var added = actual.Except(expected).ToList();
        var removed = expected.Except(actual).ToList();
        Assert.True(added.Count == 0 && removed.Count == 0,
            "تغيّرت صلاحيات نقاط النهاية عن اللقطة المراجَعة. راجع التغيير ثم حدّث الملف بتشغيل الاختبار مع UPDATE_PERMISSION_MATRIX=1 وأرفقه بالـ commit.\n" +
            $"جديد أو متغيّر:\n{Scan.Lines(added)}\nغاب أو تغيّر:\n{Scan.Lines(removed)}");
    }

    [Fact]
    public void Operations_that_change_data_are_not_behind_a_view_only_permission()
    {
        // POST/PUT/DELETE خلف «عرض» فقط = من يرى يستطيع التغيير. الاستثناءان أعلاه موثّقان؛ بقية الحالات يجب أن تكون صلاحية كتابة.
        var bad = Scan.Actions()
            .Where(a => !a.Route.StartsWith("GET") && a.Policies.Count > 0 && a.Policies.All(IsViewOnly))
            .Where(a => !ViewGatedByDesign.Contains(a.Controller.Name))
            .Select(a => $"{a.Route} → {a.Policy}").ToList();
        Assert.True(bad.Count == 0, $"عملية تغيّر بيانات خلف صلاحية عرض فقط:\n{Scan.Lines(bad)}");
    }

    [Fact]
    public void The_view_gated_controllers_list_has_no_stale_entries()
    {
        var needed = Scan.Actions().Where(a => !a.Route.StartsWith("GET") && a.Policies.Count > 0 && a.Policies.All(IsViewOnly))
            .Select(a => a.Controller.Name).ToHashSet();
        var stale = ViewGatedByDesign.Where(c => !needed.Contains(c)).ToList();
        Assert.True(stale.Count == 0, $"استثناءات لم تعد لازمة:\n{Scan.Lines(stale)}");
    }

    [Fact]
    public void The_snapshot_has_no_duplicate_routes()
    {
        var duplicates = Generate().Select(l => l.Split(" → ")[0]).GroupBy(r => r).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, $"مسار مكرر (يلتبس الموجِّه):\n{Scan.Lines(duplicates)}");
    }

    private static string SourcePath([CallerFilePath] string callerFile = "") => Path.Combine(Path.GetDirectoryName(callerFile)!, FileName);
}
