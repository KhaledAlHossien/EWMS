using System.Linq.Expressions;
using Application.DTOs.Response;
using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    public interface IMaintenanceTaskService
    {
        /// <summary>مع الموظف والقسم</summary>
        Task<MaintenanceTask?> GetByIdAsync(int id);

        /// <summary>المهام ضمن نطاق المستخدم (scope)، اختيارياً لموظف معيّن، الأحدث أولاً</summary>
        Task<(List<MaintenanceTask> Items, int TotalCount)> GetPageAsync(
            Expression<Func<MaintenanceTask, bool>> scope,
            int? userId,
            int page,
            int pageSize);

        Task<MaintenanceTask> AddAsync(MaintenanceTask task);
        Task<bool> UpdateAsync(MaintenanceTask task);
        Task<bool> DeleteAsync(MaintenanceTask task);

        /// <summary>يكمل إحصائيات الصيانة بأرقام المهام ضمن النطاق</summary>
        Task FillStatsAsync(Expression<Func<MaintenanceTask, bool>> scope, MaintenanceStatsDto stats);

        // حراسة الحذف (العلاقات Restrict)
        Task<bool> ExistsForUserAsync(int userId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
    }
}
