using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IRolePermissionService
    {
        Task AddAsync(RolePermission rolePermission);
        Task AddRangeAsync(IEnumerable<RolePermission> rolePermissions);
        Task<bool> DeleteAsync(RolePermission rolePermission);
        Task<bool> SaveChangesAsync();

        Task<List<RolePermission>> GetByRoleAsync(int roleId);
        Task<List<RolePermission>> GetByPermissionAsync(int permissionId);
        Task<bool> ExistsAsync(int roleId, int permissionId);

        Task RemoveByRoleAsync(int roleId);
        Task RemoveByPermissionAsync(int permissionId);
    }
}
