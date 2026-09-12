using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class RolePermissionService : IRolePermissionService
    {
        private readonly DataContext _context;

        public RolePermissionService(DataContext context)
        {
            _context = context;
        }

        public async Task AddAsync(RolePermission rolePermission)
        {
            await _context.RolePermissions.AddAsync(rolePermission);
            await SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<RolePermission> rolePermissions)
        {
            await _context.RolePermissions.AddRangeAsync(rolePermissions);
            await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(RolePermission rolePermission)
        {
            _context.RolePermissions.Remove(rolePermission);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<List<RolePermission>> GetByRoleAsync(int roleId)
        {
            return await _context.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();
        }

        public async Task<List<RolePermission>> GetByPermissionAsync(int permissionId)
        {
            return await _context.RolePermissions
                .Include(rp => rp.Role)
                .Where(rp => rp.PermissionId == permissionId)
                .ToListAsync();
        }

        public async Task<bool> ExistsAsync(int roleId, int permissionId)
        {
            return await _context.RolePermissions
                .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
        }

        public async Task RemoveByRoleAsync(int roleId)
        {
            var items = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            _context.RolePermissions.RemoveRange(items);
            await SaveChangesAsync();
        }

        public async Task RemoveByPermissionAsync(int permissionId)
        {
            var items = await _context.RolePermissions
                .Where(rp => rp.PermissionId == permissionId)
                .ToListAsync();

            _context.RolePermissions.RemoveRange(items);
            await SaveChangesAsync();
        }
    }
}