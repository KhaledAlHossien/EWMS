using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    public interface IDeviceTypeService
    {
        Task<DeviceType?> GetByIdAsync(int id);
        Task<List<DeviceType>> GetAllAsync();
        Task<DeviceType> AddAsync(DeviceType deviceType);
        Task<bool> UpdateAsync(DeviceType deviceType);
        Task<bool> DeleteAsync(DeviceType deviceType);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> IsUsedAsync(int id);
    }
}
