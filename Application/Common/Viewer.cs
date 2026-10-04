using Application.Interfaces;
using Domain.Entities;

namespace Application.Common;

/// <summary>
/// المستخدم الحالي مع صلاحيات دوره — ما يحتاجه أي handler ليحدد السجلات حسب حدّ كل صلاحية.
/// SuperAdmin يملك كل الصلاحيات (DbSeeder) ولا حدّ له.
/// </summary>
public sealed record Viewer(User User, IReadOnlySet<string> Permissions)
{
    public int Id => User.Id;
    public bool IsSuperAdmin => OrganizationRole.IsSystemAdmin(User);

    /// <summary>
    /// من يتصفح كل الفروع (لوحات وخرائط وإحصائيات إجازات أي فرع): يملك ViewOrganizationDashboard.
    /// الصلاحية هي المتحكم لا اسم الدور؛ مدير النظام يملكها دائماً (الـ seeder).
    /// </summary>
    public bool IsOrganizationWide => IsSuperAdmin || Permissions.Contains(AppPermissions.ViewOrganizationDashboard);
    public bool Has(string permission) => Permissions.Contains(permission);

    public static async Task<Viewer> CurrentAsync(IUserService users, IUserPermissionService permissions)
    {
        var user = await users.GetByIdAsync(users.UserId)
            ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");
        return new Viewer(user, await permissions.GetAsync(user.Id));
    }
}
