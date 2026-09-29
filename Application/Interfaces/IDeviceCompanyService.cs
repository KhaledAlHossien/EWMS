using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    public interface IDeviceCompanyService
    {
        Task<DeviceCompany?> GetByIdAsync(int id);
        Task<List<DeviceCompany>> GetAllAsync();
        Task<DeviceCompany> AddAsync(DeviceCompany company);
        Task<bool> UpdateAsync(DeviceCompany company);
        Task<bool> DeleteAsync(DeviceCompany company);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> IsUsedAsync(int id);
    }
}
