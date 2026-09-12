using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IProjectAssignmentService
    {
        Task<ProjectAssignments?> GetByIdAsync(int id);
        Task AddAsync(ProjectAssignments assignment);
        Task<bool> UpdateAsync(ProjectAssignments assignment);
        Task<bool> DeleteAsync(ProjectAssignments assignment);
        Task<bool> SaveChangesAsync();

        Task<List<ProjectAssignments>> GetByProjectAsync(int projectId);
        Task<List<ProjectAssignments>> GetByUserAsync(int userId);
        Task<ProjectAssignments?> GetActiveAssignmentAsync(int projectId, int userId);
    }
}
