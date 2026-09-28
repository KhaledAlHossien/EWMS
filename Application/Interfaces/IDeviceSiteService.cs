using Domain.Entities;

namespace Application.Interfaces
{
    public interface IDeviceSiteService
    {
        Task<DeviceSite?> GetByIdAsync(int id);
        Task<List<DeviceSite>> GetAllAsync();
        Task<List<DeviceSite>> GetBySiteAsync(int siteId);
        Task<List<DeviceSite>> GetByDeviceAsync(int deviceId);
        Task<DeviceSite> AddAsync(DeviceSite deviceSite);
        Task<bool> UpdateAsync(DeviceSite deviceSite);
        Task<bool> DeleteAsync(DeviceSite deviceSite);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsLinkAsync(int deviceId, int siteId, int? excludeId = null);
    }
}
