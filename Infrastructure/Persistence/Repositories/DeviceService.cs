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

        public async Task<Device?> GetByIdAsync(int id)
        {
            return await _context.Devices.FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<Device>> GetAllAsync()
        {
            return await _context.Devices.ToListAsync();
        }

        public async Task<Device> AddAsync(Device device)
        {
            var result = await _context.Devices.AddAsync(device);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(Device device)
        {
            _context.Devices.Update(device);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(Device device)
        {
            _context.Devices.Remove(device);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Devices.AnyAsync(d => d.Id == id);
        }

        public async Task<bool> HasSiteLinksAsync(int deviceId)
        {
            return await _context.DeviceSites.AnyAsync(ds => ds.DeviceId == deviceId);
        }
    }
}
