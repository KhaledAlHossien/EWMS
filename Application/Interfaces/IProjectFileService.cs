using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IProjectFileService
    {
        Task<ProjectFile?> GetByIdAsync(int id);
        Task AddAsync(ProjectFile file);
        Task<bool> DeleteAsync(ProjectFile file);
        Task<bool> SaveChangesAsync();

        Task<List<ProjectFile>> GetByProjectAsync(int projectId);
    }
}
