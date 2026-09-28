using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class WorkTaskService : IWorkTaskService
    {
        private readonly DataContext _context;

        public WorkTaskService(DataContext context)
        {
            _context = context;
        }

        private IQueryable<WorkTask> WithDetails() => _context.WorkTasks
            .Include(t => t.Branch)
            .Include(t => t.Assignments).ThenInclude(a => a.User).ThenInclude(u => u.Department)
            .Include(t => t.Assignments).ThenInclude(a => a.User).ThenInclude(u => u.Office);

        public async Task<WorkTask?> GetByIdAsync(int id)
        {
            return await WithDetails().FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<WorkTask>> GetAllAsync(int? branchId = null)
        {
            return await WithDetails()
                .Where(t => branchId == null || t.BranchId == branchId)
                .OrderBy(t => t.BranchId).ThenBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<WorkTask>> GetByBranchAsync(int branchId, bool activeOnly)
        {
            return await _context.WorkTasks
                .Include(t => t.Branch)
                .Include(t => t.Assignments)
                .Where(t => t.BranchId == branchId && (!activeOnly || t.IsActive))
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<List<WorkTask>> GetForUserAsync(int userId, int branchId)
        {
            return await _context.WorkTasks
                .Include(t => t.Branch)
                .Include(t => t.Assignments)
                .Where(t => t.IsActive && t.BranchId == branchId && t.Assignments.Any(a => a.UserId == userId))
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<bool> IsAssignedAsync(int workTaskId, int userId)
        {
            return await _context.UserWorkTasks.AnyAsync(a => a.WorkTaskId == workTaskId && a.UserId == userId);
        }

        public async Task<bool> ExistsByNameAsync(int branchId, string name, int? excludeId = null)
        {
            return await _context.WorkTasks.AnyAsync(t =>
                t.BranchId == branchId && t.Name == name && (excludeId == null || t.Id != excludeId));
        }

        public async Task<WorkTask> AddAsync(WorkTask task)
        {
            await _context.WorkTasks.AddAsync(task);
            await _context.SaveChangesAsync();
            return task;
        }

        public async Task UpdateAsync(WorkTask task)
        {
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(WorkTask task)
        {
            _context.WorkTasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }
}
