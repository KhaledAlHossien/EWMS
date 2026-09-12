using Domain.Entities;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IProjectService
    {
        Task<Project?> GetByIdAsync(int id);
        Task<List<Project>> GetAllAsync();
        Task AddAsync(Project project);
        Task<bool> UpdateAsync(Project project);
        Task<bool> DeleteAsync(Project project);
        Task<bool> SaveChangesAsync();

        Task<Project?> GetWithDetailsAsync(int id); // مع User + Department
        Task<List<Project>> GetByDepartmentAsync(int departmentId);
        Task<List<Project>> GetByStatusAsync(ProjectStatus status);
        Task<List<Project>> GetByCreatorAsync(int userId);
        Task<bool> ExistsAsync(int id);
    }
}
