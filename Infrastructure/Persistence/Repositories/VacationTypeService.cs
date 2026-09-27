using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class VacationTypeService : IVacationTypeService
    {
        private readonly DataContext _context;

        public VacationTypeService(DataContext context)
        {
            _context = context;
        }

        public async Task<List<VacationType>> GetAllAsync()
            => await _context.VacationType
                .OrderBy(vt => vt.Id)
                .ToListAsync();

        public async Task<VacationType?> GetByIdAsync(int id)
            => await _context.VacationType
                .FirstOrDefaultAsync(vt => vt.Id == id);

        public async Task AddAsync(VacationType vacationType)
        {
            await _context.VacationType.AddAsync(vacationType);
            await SaveChangesAsync();
        }

        public async Task UpdateAsync(VacationType vacationType)
        {
            _context.VacationType.Update(vacationType);
            await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(VacationType vacationType)
        {
            _context.VacationType.Remove(vacationType);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
            => (await _context.SaveChangesAsync()) > 0;

        public async Task<bool> ExistsAsync(int id)
            => await _context.VacationType.AnyAsync(vt => vt.Id == id);

        public async Task<bool> IsNameUniqueAsync(string name, int? excludeId = null)
            => !await _context.VacationType
                .AnyAsync(vt => vt.Name == name && vt.Id != excludeId);

        public async Task<bool> IsUsedAsync(int vacationTypeId)
            => await _context.Vacation
                .AnyAsync(v => v.VacationTypeId == vacationTypeId);
    }
}