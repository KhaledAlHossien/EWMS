using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ProjectService : IProjectService
    {
        private readonly DataContext _context;

        public ProjectService(DataContext context)
        {
            _context = context;
        }

        public async Task<Project?> GetByIdAsync(int id)
        {
            return await _context.Projects.FindAsync(id);
        }

        public async Task<List<Project>> GetAllAsync()
        {
            return await _context.Projects
                .Include(p => p.User)
                .Include(p => p.Department)
                .ToListAsync();
        }

        public async Task AddAsync(Project project)
        {
            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.Projects.AddAsync(project);
            await SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(Project project)
        {
            project.UpdatedAt = DateTime.UtcNow;
            _context.Projects.Update(project);
            return await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Project project)
        {
            _context.Projects.Remove(project);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<Project?> GetWithDetailsAsync(int id)
        {
            return await _context.Projects
                .Include(p => p.User)
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<Project>> GetByDepartmentAsync(int departmentId)
        {
            return await _context.Projects
                .Include(p => p.User)
                .Include(p => p.Department)
                .Where(p => p.CurrentDepartmentId == departmentId)
                .ToListAsync();
        }

        public async Task<List<Project>> GetByStatusAsync(ProjectStatus status)
        {
            return await _context.Projects
                .Include(p => p.User)
                .Include(p => p.Department)
                .Where(p => p.Status == status)
                .ToListAsync();
        }

        public async Task<List<Project>> GetByCreatorAsync(int userId)
        {
            return await _context.Projects
                .Include(p => p.Department)
                .Where(p => p.CreatedById == userId)
                .ToListAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Projects.AnyAsync(p => p.Id == id);
        }
    }
}