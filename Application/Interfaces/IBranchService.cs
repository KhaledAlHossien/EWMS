using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IBranchService
    {
        Task<Branch?> GetByIdAsync(int id);
        Task<List<Branch>> GetAllAsync();
        Task<Branch> AddAsync(Branch branch);
        Task<bool> UpdateAsync(Branch branch);
        Task<bool> DeleteAsync(Branch branch);
        Task<bool> SaveChangesAsync();

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> HasDepartmentsAsync(int branchId);
        Task<bool> HasUsersAsync(int branchId);
    }
}
