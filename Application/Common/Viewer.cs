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
    public bool Has(string permission) => Permissions.Contains(permission);

    public static async Task<Viewer> CurrentAsync(IUserService users, IUserPermissionService permissions)
    {
        var user = await users.GetByIdAsync(users.UserId)
            ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");
        return new Viewer(user, await permissions.GetAsync(user.Id));
    }
}
