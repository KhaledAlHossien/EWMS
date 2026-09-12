using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class PermissionService : IPermissionService
    {
        private readonly DataContext _context;

        public PermissionService(DataContext context)
        {
            _context = context;
        }

        public async Task<Permission?> GetByIdAsync(int id)
        {
            return await _context.Permissions.FindAsync(id);
        }

        public async Task<List<Permission>> GetAllAsync()
        {
            return await _context.Permissions.ToListAsync();
        }

        public async Task AddAsync(Permission permission)
        {
            await _context.Permissions.AddAsync(permission);
            await SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(Permission permission)
        {
            _context.Permissions.Update(permission);
            return await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Permission permission)
        {
            _context.Permissions.Remove(permission);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(string name, int? excludeId = null)
        {
            return await _context.Permissions
                .AnyAsync(p => p.Name == name && p.Id != excludeId);
        }

        public async Task<bool> IsPermissionUsedAsync(int permissionId)
        {
            return await _context.RolePermissions
                .AnyAsync(rp => rp.PermissionId == permissionId);
        }
    }
}