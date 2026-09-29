using System.Linq.Expressions;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    public interface IMaintenanceRequestService
    {
        /// <summary>مع الفني والقسم والنوع والعطل والشركة والحالة</summary>
        Task<MaintenanceRequest?> GetByIdAsync(int id);

        /// <summary>البحث ضمن نطاق المستخدم (scope) مع الفلاتر، الأحدث أولاً</summary>
        Task<(List<MaintenanceRequest> Items, int TotalCount)> SearchAsync(
            Expression<Func<MaintenanceRequest, bool>> scope,
            MaintenanceRequestFilterDto filter,
            int page,
            int pageSize);

        /// <summary>الفنيون الذين لهم طلبات ضمن النطاق (لقائمة فلتر الفني)</summary>
        Task<List<TechnicianOptionDto>> GetTechniciansAsync(Expression<Func<MaintenanceRequest, bool>> scope);

        Task<MaintenanceRequest> AddAsync(MaintenanceRequest request);
        Task<bool> UpdateAsync(MaintenanceRequest request);
        Task<bool> DeleteAsync(MaintenanceRequest request);

        // حراسة الحذف (العلاقات Restrict)
        Task<bool> ExistsForUserAsync(int userId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
    }
}
