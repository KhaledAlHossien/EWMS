using Domain.Entities;

namespace Application.Interfaces
{
    public sealed record DeviceWithCount(Device Device, int Installations);

    public interface IDeviceService
    {
        Task<Device?> GetByIdAsync(int id);
        Task<DeviceWithCount?> GetWithCountAsync(int id);
        Task<List<DeviceWithCount>> GetAllWithCountsAsync();
        /// <summary>الجهاز بالاسم والموديل (مطابقة تامة)</summary>
        Task<Device?> FindAsync(string name, string model);

        Task<Device> AddAsync(Device device);
        Task UpdateAsync(Device device, byte[]? rowVersion);
        Task DeleteAsync(Device device);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAndModelAsync(string name, string model, int? excludeId = null);
        Task<bool> HasSiteLinksAsync(int deviceId);
    }
}
