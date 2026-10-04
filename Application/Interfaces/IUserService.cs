using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetByIdAsync(int id);
        Task<List<User>> GetAllAsync();
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task<bool> DeleteAsync(User user);
        Task<bool> SaveChangesAsync();

        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetWithDetailsAsync(int id); // مع Role + Department + Office + Branch
        Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null);
        Task<bool> IsPersonalIdNumberUniqueAsync(string personalIdNumber, int? excludeId = null);
        Task<List<User>> GetByDepartmentAsync(int departmentId);
        Task<List<User>> GetByOfficeAsync(int officeId);
        Task<List<User>> GetByBranchAsync(int branchId);

        // ===== المستخدم الحالي =====
        int UserId { get; }

    }
}
