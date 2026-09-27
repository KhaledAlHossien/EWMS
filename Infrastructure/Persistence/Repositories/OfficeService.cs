using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class OfficeService : IOfficeService
    {
        private readonly DataContext _context;

        public OfficeService(DataContext context)
        {
            _context = context;
        }

        public async Task<Office?> GetByIdAsync(int id)
        {
            return await _context.Offices
                .Include(o => o.Department)
                    .ThenInclude(d => d.Branch)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<List<Office>> GetAllAsync()
        {
            return await _context.Offices
                .Include(o => o.Department)
                    .ThenInclude(d => d.Branch)
                .ToListAsync();
        }

        public async Task<Office> AddAsync(Office office)
        {
            var result = await _context.Offices.AddAsync(office);
            await SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(Office office)
        {
            _context.Offices.Update(office);
            return await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Office office)
        {
            _context.Offices.Remove(office);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Offices.AnyAsync(o => o.Id == id);
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
        {
            return await _context.Offices
                .AnyAsync(o => o.Name == name && o.Id != excludeId);
        }

        public async Task<bool> HasUsersAsync(int officeId)
        {
            return await _context.Users.AnyAsync(u => u.OfficeId == officeId);
        }

        public async Task<List<Office>> GetByDepartmentAsync(int departmentId)
        {
            return await _context.Offices
                .Where(o => o.DepartmentId == departmentId)
                .ToListAsync();
        }
    }
}
