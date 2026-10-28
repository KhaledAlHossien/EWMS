using System.Text.RegularExpressions;
using Application.Common;

namespace Architecture.Tests;

/// <summary>
/// واجهة الـ API: كل نقطة نهاية خلف صلاحية مسمّاة (قرار المستخدم 2026-10-03: لا شيء مفتوح لكل مسجَّل الدخول)،
/// والمتحكمات رقيقة (تستدعي MediatR فقط)، وكتالوج الصلاحيات متّسق.
/// </summary>
public partial class ApiContractTests
{
    /// <summary>الأساسيات الهوياتية الوحيدة بلا سياسة صلاحية (docs/architecture/auth-and-permissions.md «Nothing is open to every signed-in user»)</summary>
    private static readonly HashSet<string> IdentityBasics = ["Auth.Login", "Auth.ChangePassword", "Auth.Logout", "Auth.Me", "Auth.Permissions", "Auth.Renew"];

    /// <summary>دين معلن: متحكمان يتجاوزان قاعدة «IMediator فقط». يُنقل المنطق إلى معالج ثم يُحذف الاستثناء (الاختبار يفشل إن بقي بلا حاجة).</summary>
    private static readonly Dictionary<string, string[]> ThinControllerDebt = new()
    {
        ["AuthController"] = ["IAuthService", "IMediator"],        // الدخول والخروج وكلمة السر تمرّ بخدمة الهوية مباشرة
        ["VacationsController"] = ["IMediator", "IUserService"]   // يقرأ معرّف المستخدم الحالي لمنع طلب الغير (يُنقل إلى المعالج)
    };

    private static HashSet<string> KnownPolicies() =>
        AppPermissions.All.Select(p => p.Name).Concat(AppPermissions.AnyOf.Keys).ToHashSet();

    [Fact]
    public void Every_endpoint_is_behind_a_named_permission_policy_except_the_identity_basics()
    {
        var withoutPolicy = Scan.Actions().Where(a => a.Policy == null).Select(a => a.Name).ToHashSet();

        var unexpected = withoutPolicy.Except(IdentityBasics).ToList();
        Assert.True(unexpected.Count == 0,
            $"نقاط نهاية بلا سياسة صلاحية. أضف [Authorize(Policy = \"...\")] (صلاحية في AppPermissions)، أو افحص الصلاحية داخل المعالج مع سياسة «أيّ من» في AppPermissions.AnyOf:\n{Scan.Lines(unexpected)}");

        var stale = IdentityBasics.Except(withoutPolicy).ToList();
        Assert.True(stale.Count == 0, $"استثناءات لم تعد لازمة، احذفها من القائمة:\n{Scan.Lines(stale)}");
    }

    [Fact]
    public void Only_the_login_endpoint_is_anonymous()
    {
        var open = Scan.Actions().Where(a => a.AllowsAnonymous || !a.Authenticated).Select(a => a.Name).ToList();
        Assert.Equal(["Auth.Login"], open);
    }

    [Fact]
    public void Every_policy_used_by_a_controller_exists()
    {
        var known = KnownPolicies();
        var unknown = Scan.Actions().SelectMany(a => a.Policies.Where(p => !known.Contains(p)).Select(p => $"{a.Name} → {p}")).ToList();
        Assert.True(unknown.Count == 0, $"سياسات غير معرّفة في AppPermissions (لن تُسجَّل فتفشل كل الطلبات عليها):\n{Scan.Lines(unknown)}");
    }

    [Fact]
    public void Controllers_are_thin_and_only_send_to_MediatR()
    {
        var problems = new List<string>();
        foreach (var controller in Scan.Controllers())
        {
            var dependencies = controller.GetConstructors().Single().GetParameters().Select(p => p.ParameterType.Name).Order().ToArray();
            var allowed = ThinControllerDebt.TryGetValue(controller.Name, out var debt) ? debt.Order().ToArray() : ["IMediator"];
            if (!dependencies.SequenceEqual(allowed))
                problems.Add($"{controller.Name}({string.Join(", ", dependencies)}) والمسموح ({string.Join(", ", allowed)})");
        }
        Assert.True(problems.Count == 0, $"المتحكم يعتمد على أكثر من IMediator (المنطق في المعالجات):\n{Scan.Lines(problems)}");
    }

    [Fact]
    public void The_thin_controller_debt_list_has_no_stale_entries()
    {
        var names = Scan.Controllers().Select(c => c.Name).ToHashSet();
        var stale = ThinControllerDebt.Keys.Where(k => !names.Contains(k)).ToList();
        Assert.True(stale.Count == 0, $"متحكمات في قائمة الدين لم تعد موجودة:\n{Scan.Lines(stale)}");
    }

    // ───────── كتالوج الصلاحيات ─────────

    [Fact]
    public void Permission_names_are_unique_well_formed_and_described_in_Arabic()
    {
        var names = AppPermissions.All.Select(p => p.Name).ToList();
        var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, $"صلاحيات مكررة:\n{Scan.Lines(duplicates)}");

        var malformed = names.Where(n => !PermissionName().IsMatch(n)).ToList();
        Assert.True(malformed.Count == 0, $"اسم صلاحية يجب أن يكون PascalCase بالحروف اللاتينية:\n{Scan.Lines(malformed)}");

        var undescribed = AppPermissions.All.Where(p => !ArabicLetter().IsMatch(p.Description)).Select(p => p.Name).ToList();
        Assert.True(undescribed.Count == 0, $"وصف عربي مطلوب لكل صلاحية (يظهر في صفحة الأدوار):\n{Scan.Lines(undescribed)}");
    }

    [Fact]
    public void Any_of_policies_point_at_existing_permissions_and_do_not_shadow_one()
    {
        var names = AppPermissions.All.Select(p => p.Name).ToHashSet();
        var shadowing = AppPermissions.AnyOf.Keys.Where(names.Contains).ToList();
        Assert.True(shadowing.Count == 0, $"سياسة «أيّ من» بنفس اسم صلاحية:\n{Scan.Lines(shadowing)}");

        var missing = AppPermissions.AnyOf.SelectMany(kv => kv.Value.Where(v => !names.Contains(v)).Select(v => $"{kv.Key} → {v}")).ToList();
        Assert.True(missing.Count == 0, $"سياسة «أيّ من» تشير إلى صلاحية غير موجودة:\n{Scan.Lines(missing)}");

        var empty = AppPermissions.AnyOf.Where(kv => kv.Value.Length < 2).Select(kv => kv.Key).ToList();
        Assert.True(empty.Count == 0, $"سياسة «أيّ من» بصلاحية واحدة لا معنى لها (استعمل الصلاحية نفسها):\n{Scan.Lines(empty)}");
    }

    [Fact]
    public void Permission_constants_match_the_catalog()
    {
        var names = AppPermissions.All.Select(p => p.Name).ToHashSet();
        var constants = typeof(AppPermissions).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(string)).ToList();
        var wrong = constants.Where(f => (string)f.GetRawConstantValue()! != f.Name || !names.Contains(f.Name)).Select(f => f.Name).ToList();
        Assert.True(wrong.Count == 0, $"ثابت صلاحية لا يطابق اسمه قيمته أو غير موجود في الكتالوج:\n{Scan.Lines(wrong)}");
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]+$")]
    private static partial Regex PermissionName();

    [GeneratedRegex("[؀-ۿ]")]
    private static partial Regex ArabicLetter();
}
