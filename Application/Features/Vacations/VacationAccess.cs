using Application.Common;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Vacations
{
    /// <summary>
    /// من يرى أي إجازة ومن يوافق عليها — Role-Permission فقط، وحدّ كل صلاحية ثابت (قرار المستخدم 2026-10-03):
    /// - ViewVacations: إجازاتي. ViewDepartmentVacations: إجازات قسمي. ViewBranchVacations: إجازات فرعي.
    /// - ApproveVacationFirst / ApproveVacationFinal: المرحلة الأولى / الاعتماد النهائي لإجازات موظفي فرعي
    ///   (تُمنح لإداري الفرع، لا لرئيس القسم بالضرورة)، ويرى صاحبها إجازات فرعه ليقرر.
    /// - SuperAdmin: كل شيء.
    /// </summary>
    internal static class VacationAccess
    {
        public static bool CanApproveAny(Viewer v) =>
            v.Has(AppPermissions.ApproveVacationFirst) || v.Has(AppPermissions.ApproveVacationFinal);

        /// <summary>أي صلاحية عرض أو موافقة على الإجازات</summary>
        public static bool CanUseVacations(Viewer v) =>
            v.IsSuperAdmin || CanApproveAny(v)
            || v.Has(AppPermissions.ViewVacations)
            || v.Has(AppPermissions.ViewDepartmentVacations)
            || v.Has(AppPermissions.ViewBranchVacations);

        public static bool CanSeeBranch(Viewer v, int? branchId) =>
            branchId != null && v.User.BranchId == branchId
            && (v.Has(AppPermissions.ViewBranchVacations) || CanApproveAny(v));

        public static bool CanSeeDepartment(Viewer v, int? departmentId) =>
            departmentId != null && v.User.DepartmentId == departmentId && v.Has(AppPermissions.ViewDepartmentVacations);

        public static bool CanView(Viewer v, Vacation vacation) =>
            v.IsSuperAdmin
            || vacation.UserId == v.Id
            || CanSeeBranch(v, vacation.BranchId)
            || CanSeeDepartment(v, vacation.DepartmentId);

        /// <summary>عرض كل إجازات مستخدم آخر (صفحة موظف)</summary>
        public static bool CanViewUser(Viewer v, User target) =>
            v.IsSuperAdmin
            || target.Id == v.Id
            || CanSeeBranch(v, target.BranchId)
            || CanSeeDepartment(v, target.DepartmentId);

        /// <summary>الصلاحية المطلوبة لاتخاذ القرار في المرحلة الحالية</summary>
        public static string? PermissionForStage(VacationStatus status) => status switch
        {
            VacationStatus.PendingManager => AppPermissions.ApproveVacationFirst,
            VacationStatus.PendingBranchManager => AppPermissions.ApproveVacationFinal,
            _ => null
        };
    }
}
