using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IPermissionService
    {
        Task<Permission?> GetByIdAsync(int id);
        Task<List<Permission>> GetAllAsync();
        Task AddAsync(Permission permission);
        Task<bool> UpdateAsync(Permission permission);
        Task<bool> DeleteAsync(Permission permission);
        Task<bool> SaveChangesAsync();

        Task<bool> ExistsAsync(string name, int? excludeId = null);
        Task<bool> IsPermissionUsedAsync(int permissionId);
    }
}
