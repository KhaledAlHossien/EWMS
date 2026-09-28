using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AssignedTaskService : IAssignedTaskService
    {
        private readonly DataContext _context;

        public AssignedTaskService(DataContext context)
        {
            _context = context;
        }

        public async Task<AssignedTask?> GetByIdAsync(int id)
        {
            return await _context.AssignedTasks
                .Include(t => t.Branch)
                .Include(t => t.Department)
                .Include(t => t.Office)
                .Include(t => t.AssigneeUser)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ParentTask)
                .Include(t => t.SubTasks).ThenInclude(s => s.Department)
                .Include(t => t.SubTasks).ThenInclude(s => s.Office)
                .Include(t => t.SubTasks).ThenInclude(s => s.AssigneeUser)
                .Include(t => t.SubTasks).ThenInclude(s => s.CreatedByUser)
                .Include(t => t.Activities).ThenInclude(a => a.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<AssignedTask>> GetBoardAsync(Expression<Func<AssignedTask, bool>> filter)
        {
            return await _context.AssignedTasks.AsNoTracking()
                .Where(filter)
                .Include(t => t.Branch)
                .Include(t => t.Department)
                .Include(t => t.Office)
                .Include(t => t.AssigneeUser)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ParentTask)
                .Include(t => t.SubTasks)
                .Include(t => t.Activities.Where(a => a.Type == AssignedTaskActivityType.Comment))
                .AsSplitQuery()
                .OrderByDescending(t => t.Priority).ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(AssignedTask task)
        {
            await _context.AssignedTasks.AddAsync(task);
            await _context.SaveChangesAsync();
        }

        public async Task AddActivityAsync(AssignedTaskActivity activity)
        {
            await _context.AssignedTaskActivities.AddAsync(activity);
            await _context.SaveChangesAsync();
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task DeleteAsync(AssignedTask task)
        {
            _context.AssignedTasks.Remove(task);
            await _context.SaveChangesAsync();
        }

        public Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            _context.AssignedTasks.AnyAsync(t => t.DepartmentId == departmentId);

        public Task<bool> ExistsForOfficeAsync(int officeId) =>
            _context.AssignedTasks.AnyAsync(t => t.OfficeId == officeId);

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.AssignedTasks.AnyAsync(t => t.CreatedByUserId == userId || t.AssigneeUserId == userId)
            || await _context.AssignedTaskActivities.AnyAsync(a => a.UserId == userId);
    }
}
