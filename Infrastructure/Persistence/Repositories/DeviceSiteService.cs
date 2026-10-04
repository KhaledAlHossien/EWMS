using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DeviceSiteService : IDeviceSiteService
    {
        private readonly DataContext _context;

        public DeviceSiteService(DataContext context)
        {
            _context = context;
        }

        public async Task<DeviceSite?> GetByIdAsync(int id)
        {
            return await _context.DeviceSites
                .Include(ds => ds.Device)
                .Include(ds => ds.Site)
                .FirstOrDefaultAsync(ds => ds.Id == id);
        }

        public async Task<List<DeviceSite>> GetAllAsync()
        {
            return await _context.DeviceSites
                .Include(ds => ds.Device)
                .Include(ds => ds.Site)
                .ToListAsync();
        }

        public async Task<List<DeviceSite>> GetBySiteAsync(int siteId)
        {
            return await _context.DeviceSites
                .Include(ds => ds.Device)
                .Include(ds => ds.Site)
                .Where(ds => ds.SiteId == siteId)
                .ToListAsync();
        }

        public async Task<List<DeviceSite>> GetByDeviceAsync(int deviceId)
        {
            return await _context.DeviceSites
                .Include(ds => ds.Device)
                .Include(ds => ds.Site)
                .Where(ds => ds.DeviceId == deviceId)
                .ToListAsync();
        }

        public async Task<DeviceSite> AddAsync(DeviceSite deviceSite)
        {
            var result = await _context.DeviceSites.AddAsync(deviceSite);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(DeviceSite deviceSite)
        {
            _context.DeviceSites.Update(deviceSite);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(DeviceSite deviceSite)
        {
            _context.DeviceSites.Remove(deviceSite);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.DeviceSites.AnyAsync(ds => ds.Id == id);
        }
    }
}
