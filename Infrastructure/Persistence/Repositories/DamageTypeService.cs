using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DamageTypeService : IDamageTypeService
    {
        private readonly DataContext _context;

        public DamageTypeService(DataContext context)
        {
            _context = context;
        }

        public async Task<DamageType?> GetByIdAsync(int id) =>
            await _context.DamageTypes.FirstOrDefaultAsync(x => x.Id == id);

        public async Task<List<DamageType>> GetAllAsync() =>
            await _context.DamageTypes.OrderBy(x => x.Name).ToListAsync();

        public async Task<DamageType> AddAsync(DamageType damageType)
        {
            var result = await _context.DamageTypes.AddAsync(damageType);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(DamageType damageType)
        {
            _context.DamageTypes.Update(damageType);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(DamageType damageType)
        {
            _context.DamageTypes.Remove(damageType);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id) =>
            await _context.DamageTypes.AnyAsync(x => x.Id == id);

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null) =>
            await _context.DamageTypes.AnyAsync(x => x.Name == name && x.Id != excludeId);

        public async Task<bool> IsUsedAsync(int id) =>
            await _context.MaintenanceRequests.AnyAsync(r => r.DamageTypeId == id);
    }
}
