using Application.Common;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.Users
{
    internal static class UserRules
    {
        /// <summary>
        /// يتحقق من الفرع/القسم/المكتب المطلوبة حسب الدور (UserPlacement) ويعيدها بعد التنظيف:
        /// ما لا يحتاجه الدور يُحذف (null) حتى لو أُرسل — مثلاً رئيس الفرع لا يُحفظ له قسم.
        /// </summary>
        public static async Task<(int? BranchId, int? DepartmentId, int? OfficeId)> EnsureUserReferencesAsync(
            IBranchService branchService,
            IDepartmentService departmentService,
            IOfficeService officeService,
            IRoleService roleService,
            int? branchId,
            int? departmentId,
            int? officeId,
            int roleId)
        {
            var role = await roleService.GetByIdAsync(roleId)
                ?? throw new KeyNotFoundException("الدور غير موجود");

            var placement = UserPlacement.For(role.Name);

            if (!placement.NeedsBranch)
                return (null, null, null);

            if (branchId is not > 0)
                throw new ArgumentException("الفرع مطلوب لهذا الدور");
            if (!await branchService.ExistsAsync(branchId.Value))
                throw new KeyNotFoundException("الفرع غير موجود");

            if (!placement.NeedsDepartment)
                return (branchId, null, null);

            if (departmentId is not > 0)
                throw new ArgumentException("القسم مطلوب لهذا الدور");
            var department = await departmentService.GetByIdAsync(departmentId.Value)
                ?? throw new KeyNotFoundException("القسم غير موجود");
            if (department.BranchId != branchId)
                throw new InvalidOperationException("القسم لا يتبع الفرع المحدد");

            if (!placement.NeedsOffice)
                return (branchId, departmentId, null);

            if (officeId is not > 0)
                throw new ArgumentException("المكتب مطلوب لهذا الدور");
            var office = await officeService.GetByIdAsync(officeId.Value)
                ?? throw new KeyNotFoundException("المكتب غير موجود");
            if (office.DepartmentId != departmentId)
                throw new InvalidOperationException("المكتب لا يتبع القسم المحدد");

            return (branchId, departmentId, officeId);
        }

        public static async Task EnsureCanManageUserAsync(
            ICurrentUserService currentUserService,
            IRoleService roleService,
            IRolePermissionService rolePermissionService,
            int? branchId,
            int roleId)
        {
            if (currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return;

            var role = await roleService.GetByIdAsync(roleId)
                ?? throw new KeyNotFoundException("الدور غير موجود");

            if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("لا يمكن لرئيس الفرع منح دور SuperAdmin");

            var rolePermissions = await rolePermissionService.GetByRoleAsync(roleId);
            if (rolePermissions.Any(rp => AppPermissions.IsBranchManagement(rp.Permission.Name)))
                throw new UnauthorizedAccessException("لا يمكن لرئيس الفرع منح صلاحية إدارة الفروع");

            if (currentUserService.BranchId != branchId)
                throw new UnauthorizedAccessException("لا يمكنك إدارة مستخدم خارج فرعك");
        }

        public static void EnsureCanChangeExistingUser(ICurrentUserService currentUserService, User user)
        {
            if (currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return;

            if (currentUserService.BranchId != user.BranchId)
                throw new UnauthorizedAccessException("لا يمكنك إدارة مستخدم خارج فرعك");

            if (user.Role?.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) == true)
                throw new UnauthorizedAccessException("لا يمكن تعديل مستخدم SuperAdmin إلا من SuperAdmin");
        }
    }
}
