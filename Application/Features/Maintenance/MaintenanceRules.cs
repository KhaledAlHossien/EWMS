using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance
{
    /// <summary>
    /// قواعد الصيانة (قرار المستخدم 2026-09-29، وُسّعت 2026-09-30):
    /// - صاحب السجل هو من أنشأه (الفني لطلب الصيانة / الموظف للمهمة)، ولرئيس قسمه نقله إلى موظف آخر من القسم.
    /// - يرى السجل: صاحبه، ورئيس قسمه (Manager بنفس القسم المحفوظ في السجل)، ورئيس فرع ذلك القسم (اطلاع فقط)، والسوبر ادمن.
    /// - يعدّله ويحذفه: صاحبه، ورئيس قسمه، والسوبر ادمن.
    /// - الصلاحية (Policy) تحدد نوع العملية، وهذه القواعد تحدد أي السجلات.
    /// </summary>
    public static class MaintenanceRules
    {
        public const int MaxPageSize = 100;

        public const string RequestEntity = "MaintenanceRequest";
        public const string TaskEntity = "MaintenanceTask";

        public static async Task<User> CurrentUserAsync(IUserService userService) =>
            await userService.GetByIdAsync(userService.UserId)
            ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

        /// <summary>التعديل والحذف: صاحب السجل، رئيس قسمه، السوبر ادمن</summary>
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

        /// <summary>العرض: من يستطيع التعديل + رئيس فرع قسم السجل</summary>
        public static bool CanView(User viewer, int ownerUserId, Domain.Entities.Department? department) =>
            CanAccess(viewer, ownerUserId, department?.Id)
            || (viewer.Role?.Name == "BranchManager" && viewer.BranchId != null && viewer.BranchId == department?.BranchId);

        public static void EnsureCanView(User viewer, int ownerUserId, Domain.Entities.Department? department, string message)
        {
            if (!CanView(viewer, ownerUserId, department))
                throw new UnauthorizedAccessException(message);
        }

        /// <summary>نقل السجل إلى موظف آخر: رئيس قسم السجل أو السوبر ادمن</summary>
        public static bool CanAssign(User viewer, int? departmentId)
        {
            var role = viewer.Role?.Name ?? "";
            return role == "SuperAdmin"
                || (role == "Manager" && viewer.DepartmentId != null && viewer.DepartmentId == departmentId);
        }

        /// <summary>
        /// يتحقق من الموظف الجديد ويعيد القسم الذي يُحفظ في السجل:
        /// رئيس القسم ينقل داخل قسمه فقط، والسوبر ادمن لأي موظف له قسم (فيتبع السجل قسم ذلك الموظف).
        /// </summary>
        public static async Task<User> ResolveAssigneeAsync(
            IUserService userService, User viewer, int? recordDepartmentId, int newUserId)
        {
            if (!CanAssign(viewer, recordDepartmentId))
                throw new UnauthorizedAccessException("تغيير الموظف المسؤول متاح لرئيس القسم فقط");

            var target = await userService.GetByIdAsync(newUserId)
                ?? throw new KeyNotFoundException("الموظف المحدد غير موجود");

            if (!target.IsActive)
                throw new InvalidOperationException("لا يمكن الإسناد إلى حساب معطّل");

            if (target.DepartmentId == null)
                throw new InvalidOperationException("الموظف المحدد لا يتبع لأي قسم");

            if (viewer.Role?.Name != "SuperAdmin" && target.DepartmentId != recordDepartmentId)
                throw new InvalidOperationException("يمكن الإسناد إلى موظفي القسم نفسه فقط");

            return target;
        }

        public static Expression<Func<MaintenanceRequest, bool>> RequestScope(User viewer)
        {
            var role = viewer.Role?.Name ?? "";
            var userId = viewer.Id;
            var departmentId = viewer.DepartmentId;
            var branchId = viewer.BranchId;

            if (role == "SuperAdmin")
                return r => true;

            if (role == "BranchManager" && branchId != null)
                return r => r.UserId == userId || (r.Department != null && r.Department.BranchId == branchId);

            if (role == "Manager" && departmentId != null)
                return r => r.UserId == userId || r.DepartmentId == departmentId;

            return r => r.UserId == userId;
        }

        public static Expression<Func<MaintenanceTask, bool>> TaskScope(User viewer)
        {
            var role = viewer.Role?.Name ?? "";
            var userId = viewer.Id;
            var departmentId = viewer.DepartmentId;
            var branchId = viewer.BranchId;

            if (role == "SuperAdmin")
                return t => true;

            if (role == "BranchManager" && branchId != null)
                return t => t.UserId == userId || (t.Department != null && t.Department.BranchId == branchId);

            if (role == "Manager" && departmentId != null)
                return t => t.UserId == userId || t.DepartmentId == departmentId;

            return t => t.UserId == userId;
        }

        public static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
            (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

        /// <summary>
        /// رقم هاتف سوري بعد حذف الفراغات والشرطات: جوال 09XXXXXXXX، أو أرضي 0 + رمز المحافظة + الرقم (9–10 خانات)،
        /// ويُقبل بالصيغة الدولية ‎+963 / 00963 بدل الصفر. مطابق لـ core/utils/phone.ts في الواجهة — عدّلهما معاً.
        /// </summary>
        private static readonly Regex PhonePattern = new(@"^(?:0|\+963|00963)[1-9]\d{7,8}$", RegexOptions.Compiled);

        public static string NormalizePhone(string? phone) => Regex.Replace(phone ?? "", @"[\s\-()]", "");

        public static bool IsValidPhone(string? phone)
        {
            var value = NormalizePhone(phone);
            return value.Length == 0 || PhonePattern.IsMatch(value);
        }

        /// <summary>رقم الطلب المعروض للعميل وفي الواجهات: MR-2026-00125 (يُشتق من المعرّف وسنة الإنشاء)</summary>
        public static string RequestNumber(int id, DateTime createdAt) => $"MR-{createdAt.Year}-{id:D5}";
    }
}
