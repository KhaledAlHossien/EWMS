using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IRoleService
    {
        Task<Role?> GetByIdAsync(int id);
        Task<List<Role>> GetAllAsync();
        Task AddAsync(Role role);
        Task<bool> UpdateAsync(Role role);
        Task<bool> DeleteAsync(Role role);
        Task<bool> SaveChangesAsync();

        Task<Role?> GetByNameAsync(string name);
        Task<bool> ExistsAsync(string name, int? excludeId = null);
        Task<bool> IsRoleUsedAsync(int roleId);
    }
}
