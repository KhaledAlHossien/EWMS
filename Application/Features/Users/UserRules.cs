using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.Users
{
    internal static class UserRules
    {
        public static async Task EnsureUserReferencesAsync(
            IBranchService branchService,
            IDepartmentService departmentService,
            IOfficeService officeService,
            IRoleService roleService,
            int branchId,
            int departmentId,
            int officeId,
            int roleId)
        {
            if (!await branchService.ExistsAsync(branchId))
                throw new KeyNotFoundException("الفرع غير موجود");

            var department = await departmentService.GetByIdAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            if (department.BranchId != branchId)
                throw new InvalidOperationException("القسم لا يتبع الفرع المحدد");

            var office = await officeService.GetByIdAsync(officeId)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            if (office.DepartmentId != departmentId)
                throw new InvalidOperationException("المكتب لا يتبع القسم المحدد");

            if (await roleService.GetByIdAsync(roleId) == null)
                throw new KeyNotFoundException("الدور غير موجود");
        }

        public static async Task EnsureCanManageUserAsync(
            ICurrentUserService currentUserService,
            IRoleService roleService,
            IRolePermissionService rolePermissionService,
            int branchId,
            int roleId)
        {
            if (currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return;

            if (currentUserService.BranchId != branchId)
                throw new UnauthorizedAccessException("لا يمكنك إدارة مستخدم خارج فرعك");

            var role = await roleService.GetByIdAsync(roleId)
                ?? throw new KeyNotFoundException("الدور غير موجود");

            if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("لا يمكن لرئيس الفرع منح دور SuperAdmin");

            var rolePermissions = await rolePermissionService.GetByRoleAsync(roleId);
            if (rolePermissions.Any(rp => rp.Permission.Name.Equals("ManageBranches", StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("لا يمكن لرئيس الفرع منح صلاحية إدارة الفروع");
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
