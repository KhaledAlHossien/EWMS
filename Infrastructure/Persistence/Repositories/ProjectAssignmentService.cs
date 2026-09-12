using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ProjectAssignmentService : IProjectAssignmentService
    {
        private readonly DataContext _context;

        public ProjectAssignmentService(DataContext context)
        {
            _context = context;
        }

        public async Task<ProjectAssignments?> GetByIdAsync(int id)
        {
            return await _context.ProjectAssignments
                .Include(pa => pa.Project)
                .Include(pa => pa.AssignedUser)
                .Include(pa => pa.AssignedByUser)
                .FirstOrDefaultAsync(pa => pa.Id == id);
        }

        public async Task AddAsync(ProjectAssignments assignment)
        {
            assignment.AssignedAt = DateTime.UtcNow;
            await _context.ProjectAssignments.AddAsync(assignment);
            await SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(ProjectAssignments assignment)
        {
            _context.ProjectAssignments.Update(assignment);
            return await SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(ProjectAssignments assignment)
        {
            _context.ProjectAssignments.Remove(assignment);
            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<List<ProjectAssignments>> GetByProjectAsync(int projectId)
        {
            return await _context.ProjectAssignments
                .Include(pa => pa.AssignedUser)
                .Include(pa => pa.AssignedByUser)
                .Where(pa => pa.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<List<ProjectAssignments>> GetByUserAsync(int userId)
        {
            return await _context.ProjectAssignments
                .Include(pa => pa.Project)
                    .ThenInclude(p => p.User)
                .Include(pa => pa.Project)
                    .ThenInclude(p => p.Department)
                .Where(pa => pa.AssignedUserId == userId)
                .ToListAsync();
        }

        public async Task<ProjectAssignments?> GetActiveAssignmentAsync(int projectId, int userId)
        {
            return await _context.ProjectAssignments
                .FirstOrDefaultAsync(pa => pa.ProjectId == projectId
                                        && pa.AssignedUserId == userId
                                        && pa.Status == AssignmentStatus.Active);
        }
    }
}
