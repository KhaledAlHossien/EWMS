using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class RoleService : IRoleService
    {
        private readonly DataContext _context;

        public RoleService(DataContext context)
        {
            _context = context;
        }

        public async Task<Role?> GetByIdAsync(int id)
        {
            return await _context.Roles.FindAsync(id);
        }

        public async Task<List<Role>> GetAllAsync()
        {
            return await _context.Roles.ToListAsync();
        }

        public async Task AddAsync(Role role)
        {
            await _context.Roles.AddAsync(role);
            await SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(Role role)
        {
            _context.Roles.Update(role);
            return await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Role role)
        {
            _context.Roles.Remove(role);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<Role?> GetByNameAsync(string name)
        {
            return await _context.Roles.FirstOrDefaultAsync(r => r.Name == name);
        }

        public async Task<bool> ExistsAsync(string name, int? excludeId = null)
        {
            return await _context.Roles
                .AnyAsync(r => r.Name == name && r.Id != excludeId);
        }

        public async Task<bool> IsRoleUsedAsync(int roleId)
        {
            return await _context.Users.AnyAsync(u => u.RoleId == roleId);
        }
    }
}