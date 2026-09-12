using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IDepartmentService
    {
        Task<Department?> GetByIdAsync(int id);
        Task<List<Department>> GetAllAsync();
        Task<Department> AddAsync(Department department);
        Task<bool> UpdateAsync(Department department);
        Task<bool> DeleteAsync(Department department);
        Task<bool> SaveChangesAsync();

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> HasUsersAsync(int departmentId);
        Task<bool> HasProjectsAsync(int departmentId);

        Task<List<Department>> GetByBranchAsync(int branchId);
    }
}
