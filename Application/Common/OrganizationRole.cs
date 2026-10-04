using Domain.Entities;

namespace Application.Common;

/// <summary>
/// لا يُستنتج أي منصب (رئيس فرع/قسم/مكتب) من الاسم أو المكان: كل شيء صلاحية (Role-Permission).
/// هنا فقط: مدير النظام العام، ومقارنة وحدة المستخدم بوحدة سجل.
/// </summary>
public static class OrganizationRole
{
    public static bool IsSystemAdmin(User user) => user.Role?.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>وحدة المستخدم نفسها هي هذا القسم (يتبع للقسم مباشرة، لا لمكتب فيه)</summary>
    public static bool IsInDepartmentItself(User user, int? departmentId) =>
        departmentId != null && user.DepartmentId == departmentId && user.OfficeId == null;

    /// <summary>وحدة المستخدم نفسها هي هذا المكتب</summary>
    public static bool IsInOffice(User user, int? officeId) =>
        officeId != null && user.OfficeId == officeId;
}
