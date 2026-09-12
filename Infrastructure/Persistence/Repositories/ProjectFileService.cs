using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ProjectFileService : IProjectFileService
    {
        private readonly DataContext _context;

        public ProjectFileService(DataContext context)
        {
            _context = context;
        }

        public async Task<ProjectFile?> GetByIdAsync(int id)
        {
            return await _context.ProjectFiles
                .Include(pf => pf.Project)
                .Include(pf => pf.User)
                .FirstOrDefaultAsync(pf => pf.Id == id);
        }

        public async Task AddAsync(ProjectFile file)
        {
            file.UploadedAt = DateTime.UtcNow;
            await _context.ProjectFiles.AddAsync(file);
            await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(ProjectFile file)
        {
            _context.ProjectFiles.Remove(file);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<List<ProjectFile>> GetByProjectAsync(int projectId)
        {
            return await _context.ProjectFiles
                .Include(pf => pf.User)
                .Where(pf => pf.ProjectId == projectId)
                .OrderByDescending(pf => pf.UploadedAt)
                .ToListAsync();
        }
    }
}