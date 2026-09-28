using Domain.Entities;

namespace Application.Interfaces
{
    public interface IDeviceService
    {
        Task<Device?> GetByIdAsync(int id);
        Task<List<Device>> GetAllAsync();
        Task<Device> AddAsync(Device device);
        Task<bool> UpdateAsync(Device device);
        Task<bool> DeleteAsync(Device device);

        Task<bool> ExistsAsync(int id);
        Task<bool> HasSiteLinksAsync(int deviceId);
    }
}
