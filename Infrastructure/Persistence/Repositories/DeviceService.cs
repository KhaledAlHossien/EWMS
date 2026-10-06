using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DeviceService : IDeviceService
    {
        private readonly DataContext _context;

        public DeviceService(DataContext context)
        {
            _context = context;
        }

        private IQueryable<DeviceWithCount> WithCounts(IQueryable<Device> devices) => devices.AsNoTracking().Select(d => new DeviceWithCount(
            d, _context.DeviceSites.Count(ds => ds.DeviceId == d.Id)));

        public async Task<Device?> GetByIdAsync(int id) => await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);

        public async Task<DeviceWithCount?> GetWithCountAsync(int id) =>
            await WithCounts(_context.Devices.Where(d => d.Id == id)).FirstOrDefaultAsync();

        public async Task<List<DeviceWithCount>> GetAllWithCountsAsync() =>
            await WithCounts(_context.Devices.OrderBy(d => d.Name).ThenBy(d => d.Model)).ToListAsync();

        public async Task<Device?> FindAsync(string name, string model) =>
            await _context.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Name == name && d.Model == model);

        public async Task<Device> AddAsync(Device device)
        {
            await _context.Devices.AddAsync(device);
            await SaveAsync();
            return device;
        }

        public async Task UpdateAsync(Device device, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(device).Property(d => d.RowVersion).OriginalValue = rowVersion;
            await SaveAsync();
        }

        public async Task DeleteAsync(Device device)
        {
            _context.Devices.Remove(device);
            await SaveAsync();
        }

        public async Task<bool> ExistsAsync(int id) => await _context.Devices.AnyAsync(d => d.Id == id);

        public async Task<bool> ExistsByNameAndModelAsync(string name, string model, int? excludeId = null) =>
            await _context.Devices.AnyAsync(d => d.Name == name && d.Model == model && d.Id != excludeId);

        public async Task<bool> HasSiteLinksAsync(int deviceId) =>
            await _context.DeviceSites.AnyAsync(ds => ds.DeviceId == deviceId);

        private Task SaveAsync() => DeviceInventorySave.SaveAsync(_context, "يوجد جهاز بنفس الاسم والموديل في الكتالوج");
    }
}
