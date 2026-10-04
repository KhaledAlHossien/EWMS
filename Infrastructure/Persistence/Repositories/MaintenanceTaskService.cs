using System.Linq.Expressions;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class MaintenanceTaskService : IMaintenanceTaskService
    {
        private readonly DataContext _context;

        public MaintenanceTaskService(DataContext context)
        {
            _context = context;
        }

        public async Task<MaintenanceTask?> GetByIdAsync(int id) =>
            await _context.MaintenanceTasks
                .Include(t => t.User)
                .Include(t => t.Department)
                .FirstOrDefaultAsync(t => t.Id == id);

        public async Task<(List<MaintenanceTask> Items, int TotalCount)> GetPageAsync(
            Expression<Func<MaintenanceTask, bool>> scope,
            int? userId,
            int page,
            int pageSize)
        {
            var query = _context.MaintenanceTasks.Where(scope);

            if (userId is int uid)
                query = query.Where(t => t.UserId == uid);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(t => t.User)
                .Include(t => t.Department)
                .AsNoTracking()
                .ToListAsync();

            return (items, total);
        }

        public async Task<MaintenanceTask> AddAsync(MaintenanceTask task)
        {
            var result = await _context.MaintenanceTasks.AddAsync(task);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(MaintenanceTask task)
        {
            // المهمة محمَّلة ومتتبَّعة مع الموظف والقسم — لا نعلّمهما كمعدَّلين
            if (_context.Entry(task).State == EntityState.Detached)
                _context.MaintenanceTasks.Update(task);

            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(MaintenanceTask task)
        {
            _context.MaintenanceTasks.Remove(task);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task FillStatsAsync(Expression<Func<MaintenanceTask, bool>> scope, MaintenanceStatsDto stats)
        {
            var query = _context.MaintenanceTasks.Where(scope);

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            stats.TotalTasks = await query.CountAsync();
            stats.TasksThisMonth = await query.CountAsync(t => t.CreatedAt >= monthStart);

            stats.TasksByUser = (await query
                    .GroupBy(t => new { t.UserId, t.User.FullName })
                    .Select(g => new MaintenanceCountDto { Id = g.Key.UserId, Name = g.Key.FullName, Count = g.Count() })
                    .ToListAsync())
                .OrderByDescending(x => x.Count)
                .ToList();
        }

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.MaintenanceTasks.AnyAsync(t => t.UserId == userId);

        public async Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            await _context.MaintenanceTasks.AnyAsync(t => t.DepartmentId == departmentId);
    }
}
