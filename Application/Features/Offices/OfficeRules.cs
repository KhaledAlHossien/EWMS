using Application.Common;
using Application.Interfaces;

namespace Application.Features.Offices
{
    internal static class OfficeRules
    {
        /// <summary>
        /// نطاق إدارة المكاتب لغير SuperAdmin:
        /// - من له قسم (رئيس القسم) → مكاتب قسمه فقط.
        /// - من لا قسم له (رئيس الفرع) → مكاتب كل الأقسام التابعة لفرعه.
        /// </summary>
        public static async Task EnsureCanManageDepartmentAsync(
            ICurrentUserService currentUserService,
            IDepartmentService departmentService,
            int departmentId,
            string message)
        {
            var me = await currentUserService.GetUserAsync();
            if (OrganizationRole.IsSystemAdmin(me))
                return;

            if (me.DepartmentId is > 0)
            {
                if (me.DepartmentId != departmentId)
                    throw new UnauthorizedAccessException(message);
                return;
            }

            var department = await departmentService.GetByIdAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم المحدد غير موجود");

            if (!OrganizationRole.InBranch(me, department.BranchId))
                throw new UnauthorizedAccessException(message);
        }
    }
}
