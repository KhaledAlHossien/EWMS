namespace Application.Common;

/// <summary>
/// حدّ صلاحيات عرض الهيكل (ViewBranches / ViewDepartments / ViewOffices / ViewUsers) — قرار المستخدم 2026-10-04:
/// يرى المستخدم فرعه فقط (وأقسامه ومكاتبه وموظفيه)، إلا من يتصفح المؤسسة كلها (ViewOrganizationDashboard أو SuperAdmin).
/// مستخدم بلا فرع وبلا نظرة مؤسسة لا يرى شيئاً.
/// </summary>
public static class StructureScope
{
    /// <summary>null = كل المؤسسة، وإلا رقم فرع المستخدم (0 = لا فرع له → لا نتائج)</summary>
    public static int? BranchOf(Viewer viewer) =>
        viewer.IsOrganizationWide ? null : viewer.User.BranchId ?? 0;

    public static bool Includes(Viewer viewer, int? branchId)
    {
        var limit = BranchOf(viewer);
        return limit == null || (branchId != null && branchId == limit);
    }

    public static void EnsureIncludes(Viewer viewer, int? branchId, string message)
    {
        if (!Includes(viewer, branchId)) throw new UnauthorizedAccessException(message);
    }
}
