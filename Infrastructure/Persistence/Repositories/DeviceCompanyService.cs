using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DeviceCompanyService : IDeviceCompanyService
    {
        private readonly DataContext _context;

        public DeviceCompanyService(DataContext context)
        {
            _context = context;
        }

        public async Task<DeviceCompany?> GetByIdAsync(int id) =>
            await _context.DeviceCompanies.FirstOrDefaultAsync(x => x.Id == id);

        public async Task<List<DeviceCompany>> GetAllAsync() =>
            await _context.DeviceCompanies.OrderBy(x => x.Name).ToListAsync();

        public async Task<DeviceCompany> AddAsync(DeviceCompany company)
        {
            var result = await _context.DeviceCompanies.AddAsync(company);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(DeviceCompany company)
        {
            _context.DeviceCompanies.Update(company);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(DeviceCompany company)
        {
            _context.DeviceCompanies.Remove(company);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id) =>
            await _context.DeviceCompanies.AnyAsync(x => x.Id == id);

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null) =>
            await _context.DeviceCompanies.AnyAsync(x => x.Name == name && x.Id != excludeId);

        public async Task<bool> IsUsedAsync(int id) =>
            await _context.MaintenanceRequests.AnyAsync(r => r.DeviceCompanyId == id);
    }
}
