using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DeviceTypeService : IDeviceTypeService
    {
        private readonly DataContext _context;

        public DeviceTypeService(DataContext context)
        {
            _context = context;
        }

        public async Task<DeviceType?> GetByIdAsync(int id) =>
            await _context.DeviceTypes.FirstOrDefaultAsync(x => x.Id == id);

        public async Task<List<DeviceType>> GetAllAsync() =>
            await _context.DeviceTypes.OrderBy(x => x.Name).ToListAsync();

        public async Task<DeviceType> AddAsync(DeviceType deviceType)
        {
            var result = await _context.DeviceTypes.AddAsync(deviceType);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(DeviceType deviceType)
        {
            _context.DeviceTypes.Update(deviceType);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(DeviceType deviceType)
        {
            _context.DeviceTypes.Remove(deviceType);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id) =>
            await _context.DeviceTypes.AnyAsync(x => x.Id == id);

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null) =>
            await _context.DeviceTypes.AnyAsync(x => x.Name == name && x.Id != excludeId);

        public async Task<bool> IsUsedAsync(int id) =>
            await _context.DeviceMaintenances.AnyAsync(d => d.DeviceTypeId == id);
    }
}
