using System.Linq.Expressions;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    public interface IMaintenanceRequestService
    {
        /// <summary>مع الفني والقسم والجهاز (ونوعه وشركته) والعطل والحالة</summary>
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

        // السجل (من فعل ماذا ومتى)
        Task AddActivityAsync(MaintenanceRequestActivity activity);
        Task<List<MaintenanceRequestActivity>> GetActivitiesAsync(int requestId);

        /// <summary>طلبات عميل موظف (الأحدث أولاً) — «أجهزتي في الصيانة»</summary>
        Task<List<MaintenanceRequest>> GetForClientAsync(int clientUserId, int take);

        // طلبات التحويل
        Task AddTransferAsync(MaintenanceTransferRequest transfer);
        Task UpdateTransferAsync(MaintenanceTransferRequest transfer);
        /// <summary>مع الطلب والمستخدمين</summary>
        Task<MaintenanceTransferRequest?> GetTransferAsync(int transferId);
        Task<MaintenanceTransferRequest?> GetPendingTransferAsync(int requestId);
        /// <summary>المعلّقة لطلبات قسم (null = كل الأقسام)، الأقدم أولاً</summary>
        Task<List<MaintenanceTransferRequest>> GetPendingTransfersAsync(int? departmentId);

        /// <summary>إحصائيات الطلبات ضمن النطاق (بدون أرقام المهام — تُملأ من خدمة المهام)</summary>
        Task<MaintenanceStatsDto> GetStatsAsync(Expression<Func<MaintenanceRequest, bool>> scope);

        // حراسة الحذف (العلاقات Restrict)
        Task<bool> ExistsForUserAsync(int userId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);
    }
}
