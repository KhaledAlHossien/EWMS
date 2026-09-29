using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    public interface IMaintenanceRequestStatusService
    {
        Task<MaintenanceRequestStatus?> GetByIdAsync(int id);
        Task<List<MaintenanceRequestStatus>> GetAllAsync();
        Task<MaintenanceRequestStatus> AddAsync(MaintenanceRequestStatus status);
        Task<bool> UpdateAsync(MaintenanceRequestStatus status);
        Task<bool> DeleteAsync(MaintenanceRequestStatus status);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> IsUsedAsync(int id);
    }
}
