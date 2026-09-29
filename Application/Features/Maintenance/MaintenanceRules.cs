using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance
{
    /// <summary>
    /// قواعد الصيانة (قرار المستخدم 2026-09-29):
    /// - صاحب السجل هو من أنشأه (الفني لطلب الصيانة / الموظف للمهمة) ويُملأ تلقائياً ولا يتغير.
    /// - يرى السجل ويعدّله ويحذفه: صاحبه، ورئيس قسمه (Manager بنفس القسم المحفوظ في السجل)، والسوبر ادمن.
    /// - الصلاحية (Policy) تحدد نوع العملية، وهذه القواعد تحدد أي السجلات.
    /// </summary>
    public static class MaintenanceRules
    {
        public const int MaxPageSize = 100;

        public static async Task<User> CurrentUserAsync(IUserService userService) =>
            await userService.GetByIdAsync(userService.UserId)
            ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

        public static bool CanAccess(User viewer, int ownerUserId, int? departmentId)
        {
            var role = viewer.Role?.Name ?? "";
            return role == "SuperAdmin"
                || viewer.Id == ownerUserId
                || (role == "Manager" && viewer.DepartmentId != null && viewer.DepartmentId == departmentId);
        }

        public static void EnsureCanAccess(User viewer, int ownerUserId, int? departmentId, string message)
        {
            if (!CanAccess(viewer, ownerUserId, departmentId))
                throw new UnauthorizedAccessException(message);
        }

        public static Expression<Func<MaintenanceRequest, bool>> RequestScope(User viewer)
        {
            var role = viewer.Role?.Name ?? "";
            var userId = viewer.Id;
            var departmentId = viewer.DepartmentId;

            if (role == "SuperAdmin")
                return r => true;

            if (role == "Manager" && departmentId != null)
                return r => r.UserId == userId || r.DepartmentId == departmentId;

            return r => r.UserId == userId;
        }

        public static Expression<Func<MaintenanceTask, bool>> TaskScope(User viewer)
        {
            var role = viewer.Role?.Name ?? "";
            var userId = viewer.Id;
            var departmentId = viewer.DepartmentId;

            if (role == "SuperAdmin")
                return t => true;

            if (role == "Manager" && departmentId != null)
                return t => t.UserId == userId || t.DepartmentId == departmentId;

            return t => t.UserId == userId;
        }

        public static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
            (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
    }
}
