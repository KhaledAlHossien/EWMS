using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance
{
    /// <summary>
    /// قواعد الصيانة — Role-Permission فقط (قرار المستخدم 2026-10-03). كل صلاحية تفتح العملية بحدّ ثابت:
    /// - عرض / تعديل / حذف / تغيير حالة طلب / إحصائيات: سجلاتي، وسجلات قسمي كلها لمن يملك ViewDepartmentMaintenance.
    /// - نقل الموظف المسؤول (AssignMaintenanceRequest / AssignMaintenanceTask): سجلات قسمي، إلى موظفي قسمي.
    /// - SuperAdmin: كل السجلات.
    /// - صاحب السجل هو من أنشأه (الفني لطلب الصيانة / الموظف للمهمة)، ويُنقل لموظف آخر بصلاحية النقل.
    /// - السجل يحفظ قسم صاحبه لحظة التسجيل (يبقى لقسمه بعد نقل الموظف).
    /// </summary>
    public static class MaintenanceRules
    {
        public const int MaxPageSize = 100;

        public const string RequestEntity = "MaintenanceRequest";
        public const string TaskEntity = "MaintenanceTask";

        /// <summary>
        /// حدّ عملية للمستخدم الحالي: All = كل السجلات (SuperAdmin)، وإلا سجلاته (OwnerId)
        /// وسجلات القسم DepartmentId إن وُجد. null حيث يُستخدم = لا يملك العملية.
        /// </summary>
        public sealed record Boundary(int OwnerId, int? DepartmentId, bool All)
        {
            public bool Includes(int ownerUserId, int? recordDepartmentId) =>
                All || ownerUserId == OwnerId || (DepartmentId != null && recordDepartmentId == DepartmentId);
        }

        /// <summary>حدود المستخدم الحالي لعمليات طلبات الصيانة أو مهامها</summary>
        public sealed record Scopes(Boundary? View, Boundary? Edit, Boundary? Delete, Boundary? Assign, Boundary? Status = null);

        public static async Task<Viewer> ViewerAsync(IUserService users, IUserPermissionService permissions) =>
            await Viewer.CurrentAsync(users, permissions);

        public static async Task<Scopes> RequestScopesAsync(IUserService users, IUserPermissionService permissions) =>
            ScopesFor(await ViewerAsync(users, permissions),
                "ViewMaintenanceRequests", "EditMaintenanceRequest", "DeleteMaintenanceRequest", "AssignMaintenanceRequest", AppPermissions.ChangeMaintenanceStatus);

        public static async Task<Scopes> TaskScopesAsync(IUserService users, IUserPermissionService permissions) =>
            ScopesFor(await ViewerAsync(users, permissions),
                "ViewMaintenanceTasks", "EditMaintenanceTask", "DeleteMaintenanceTask", "AssignMaintenanceTask");

        /// <summary>حدّ صلاحية واحدة (للعمليات التي تحتاج واحدة فقط)</summary>
        public static async Task<Boundary?> BoundaryAsync(IUserService users, IUserPermissionService permissions, string permission)
        {
            var viewer = await ViewerAsync(users, permissions);
            return permission.StartsWith("Assign") ? AssignBoundary(viewer, permission) : RecordBoundary(viewer, permission);
        }

        private static Scopes ScopesFor(Viewer v, string view, string edit, string delete, string assign, string? status = null) =>
            new(RecordBoundary(v, view), RecordBoundary(v, edit), RecordBoundary(v, delete), AssignBoundary(v, assign), status == null ? null : RecordBoundary(v, status));

        // عرض/تعديل/حذف: سجلاتي + قسمي لمن يملك ViewDepartmentMaintenance
        private static Boundary? RecordBoundary(Viewer v, string permission)
        {
            if (v.IsSuperAdmin) return new Boundary(v.Id, null, true);
            if (!v.Has(permission)) return null;
            var department = v.Has(AppPermissions.ViewDepartmentMaintenance) ? v.User.DepartmentId : null;
            return new Boundary(v.Id, department, false);
        }

        // النقل: سجلات قسمي (ومنها سجلاتي) إلى موظفي قسمي
        private static Boundary? AssignBoundary(Viewer v, string permission)
        {
            if (v.IsSuperAdmin) return new Boundary(v.Id, null, true);
            if (!v.Has(permission) || v.User.DepartmentId == null) return null;
            return new Boundary(v.Id, v.User.DepartmentId, false);
        }

        public static async Task<User> CurrentUserAsync(IUserService userService) =>
            await userService.GetByIdAsync(userService.UserId)
            ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

        public static bool In(Boundary? boundary, MaintenanceRequest r) =>
            boundary != null && boundary.Includes(r.UserId, r.DepartmentId);

        public static bool In(Boundary? boundary, MaintenanceTask t) =>
            boundary != null && boundary.Includes(t.UserId, t.DepartmentId);

        public static void Ensure(bool allowed, string message)
        {
            if (!allowed) throw new UnauthorizedAccessException(message);
        }

        /// <summary>
        /// يتحقق من الموظف الجديد للسجل: فعّال، يتبع لقسم، ومن قسم صاحب صلاحية النقل (SuperAdmin: أي قسم).
        /// السجل يتبع بعدها قسم ذلك الموظف.
        /// </summary>
        public static async Task<User> ResolveAssigneeAsync(IUserService userService, Boundary? assign, int newUserId)
        {
            Ensure(assign != null, "لا تملك صلاحية تغيير الموظف المسؤول");

            var target = await userService.GetByIdAsync(newUserId)
                ?? throw new KeyNotFoundException("الموظف المحدد غير موجود");

            if (!target.IsActive)
                throw new InvalidOperationException("لا يمكن الإسناد إلى حساب معطّل");

            if (target.DepartmentId == null)
                throw new InvalidOperationException("الموظف المحدد لا يتبع لأي قسم");

            if (!assign!.All && target.DepartmentId != assign.DepartmentId)
                throw new InvalidOperationException("يمكنك الإسناد إلى موظفي قسمك فقط");

            return target;
        }

        /// <summary>الموظفون الذين يمكن الإسناد إليهم: موظفو قسمي (SuperAdmin: القسم المحدد أو كل من له قسم)</summary>
        public static async Task<List<User>> AssigneesAsync(IUserService userService, Boundary? assign, int? departmentId = null)
        {
            if (assign == null) return [];

            var users = assign.All
                ? departmentId is int d ? await userService.GetByDepartmentAsync(d) : await userService.GetAllAsync()
                : assign.DepartmentId is int own && (departmentId == null || departmentId == own)
                    ? await userService.GetByDepartmentAsync(own)
                    : [];

            return users.Where(u => u.IsActive && u.DepartmentId != null).ToList();
        }

        public static Expression<Func<MaintenanceRequest, bool>> RequestFilter(Boundary? b)
        {
            if (b == null) return r => false;
            if (b.All) return r => true;
            var (ownerId, departmentId) = (b.OwnerId, b.DepartmentId);
            return departmentId != null
                ? r => r.UserId == ownerId || r.DepartmentId == departmentId
                : r => r.UserId == ownerId;
        }

        public static Expression<Func<MaintenanceTask, bool>> TaskFilter(Boundary? b)
        {
            if (b == null) return t => false;
            if (b.All) return t => true;
            var (ownerId, departmentId) = (b.OwnerId, b.DepartmentId);
            return departmentId != null
                ? t => t.UserId == ownerId || t.DepartmentId == departmentId
                : t => t.UserId == ownerId;
        }

        public static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
            (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

        /// <summary>
        /// رقم هاتف سوري بعد حذف الفراغات والشرطات: جوال 09XXXXXXXX، أو أرضي 0 + رمز المحافظة + الرقم (9–10 خانات)،
        /// ويُقبل بالصيغة الدولية ‎+963 / 00963 بدل الصفر. القاعدة نفسها في Application/Common/PhoneRules (مشتركة مع هاتف المستخدم).
        /// </summary>
        public static string NormalizePhone(string? phone) => PhoneRules.Normalize(phone);

        public static bool IsValidPhone(string? phone) => PhoneRules.IsValid(phone);

        /// <summary>رقم الطلب المعروض للعميل وفي الواجهات: MR-2026-00125 (يُشتق من المعرّف وسنة الإنشاء)</summary>
        public static string RequestNumber(int id, DateTime createdAt) => $"MR-{createdAt.Year}-{id:D5}";
    }
}
