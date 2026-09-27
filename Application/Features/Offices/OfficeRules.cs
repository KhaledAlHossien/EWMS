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
            if (currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return;

            if (currentUserService.DepartmentId > 0)
            {
                if (currentUserService.DepartmentId != departmentId)
                    throw new UnauthorizedAccessException(message);
                return;
            }

            var department = await departmentService.GetByIdAsync(departmentId)
                ?? throw new KeyNotFoundException("القسم المحدد غير موجود");

            if (currentUserService.BranchId <= 0 || department.BranchId != currentUserService.BranchId)
                throw new UnauthorizedAccessException(message);
        }
    }
}
