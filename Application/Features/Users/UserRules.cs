using Application.Common;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.Users
{
    internal static class UserRules
    {
        /// <summary>
        /// مكان الموظف يُحدَّد في نموذج الموظف نفسه، والدور قالب صلاحيات فقط (قرار المستخدم 2026-10-03).
        /// كل الحقول اختيارية (فرع فقط، أو فرع + قسم، أو فرع + قسم + مكتب) ولا تعتمد على الدور،
        /// والأعلى يُشتق من الأدق: المكتب يحدد قسمه، والقسم يحدد فرعه، ويُرفض أي تعارض.
        /// مدير النظام (SuperAdmin) لا يتبع لأي وحدة.
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

            if (role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return (null, null, null);

            int? office = officeId is > 0 ? officeId : null;
            int? department = departmentId is > 0 ? departmentId : null;
            int? branch = branchId is > 0 ? branchId : null;

            if (office != null)
            {
                var o = await officeService.GetByIdAsync(office.Value)
                    ?? throw new KeyNotFoundException("المكتب غير موجود");
                if (department != null && department != o.DepartmentId)
                    throw new InvalidOperationException("المكتب لا يتبع القسم المحدد");
                department = o.DepartmentId;
            }

            if (department != null)
            {
                var d = await departmentService.GetByIdAsync(department.Value)
                    ?? throw new KeyNotFoundException("القسم غير موجود");
                if (branch != null && branch != d.BranchId)
                    throw new InvalidOperationException("القسم لا يتبع الفرع المحدد");
                branch = d.BranchId;
            }
            else if (branch != null && !await branchService.ExistsAsync(branch.Value))
            {
                throw new KeyNotFoundException("الفرع غير موجود");
            }

            return (branch, department, office);
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
                throw new UnauthorizedAccessException("لا يمكنك منح دور SuperAdmin");

            var rolePermissions = await rolePermissionService.GetByRoleAsync(roleId);
            if (rolePermissions.Any(rp => AppPermissions.IsBranchManagement(rp.Permission.Name)))
                throw new UnauthorizedAccessException("لا يمكنك منح صلاحية إدارة الفروع");

            if (currentUserService.BranchId != branchId)
                throw new UnauthorizedAccessException("لا يمكنك إدارة مستخدم خارج فرعك");
        }

        /// <summary>الرقم الذاتي: بلا فراغات وبحروف كبيرة (m201160 = M201160)، والفارغ = null</summary>
        public static string? NormalizePersonalIdNumber(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

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
