using System.Linq.Expressions;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class MaintenanceRequestService : IMaintenanceRequestService
    {
        private readonly DataContext _context;

        public MaintenanceRequestService(DataContext context)
        {
            _context = context;
        }

        private IQueryable<MaintenanceRequest> WithDetails() => _context.MaintenanceRequests
            .Include(r => r.User)
            .Include(r => r.Department)
            .Include(r => r.DeviceType)
            .Include(r => r.DamageType)
            .Include(r => r.DeviceCompany)
            .Include(r => r.MaintenanceRequestStatus)
            .Include(r => r.DeliverySigner);

        public async Task<MaintenanceRequest?> GetByIdAsync(int id) =>
            await WithDetails().FirstOrDefaultAsync(r => r.Id == id);

        public async Task<(List<MaintenanceRequest> Items, int TotalCount)> SearchAsync(
            Expression<Func<MaintenanceRequest, bool>> scope,
            MaintenanceRequestFilterDto filter,
            int page,
            int pageSize)
        {
            var query = _context.MaintenanceRequests.Where(scope);

            // "يبدأ بـ" تُترجم إلى LIKE 'x%' فتستخدم الفهرس (Index Seek)
            if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            {
                var serial = filter.SerialNumber.Trim();
                query = query.Where(r => r.SerialNumber.StartsWith(serial));
            }

            if (!string.IsNullOrWhiteSpace(filter.Model))
            {
                var model = filter.Model.Trim();
                query = query.Where(r => r.Model.StartsWith(model));
            }

            // الاسم قد يُكتب من وسطه (مثل الكنية) فنبحث بـ"يحتوي" — يمسح فهرس الاسم بدل الجدول كاملاً
            if (!string.IsNullOrWhiteSpace(filter.ClientName))
            {
                var clientName = filter.ClientName.Trim();
                query = query.Where(r => r.ClientName.Contains(clientName));
            }

            if (filter.DeviceCompanyId is int companyId)
                query = query.Where(r => r.DeviceCompanyId == companyId);

            if (filter.TechnicianId is int technicianId)
                query = query.Where(r => r.UserId == technicianId);

            if (filter.DeviceTypeId is int deviceTypeId)
                query = query.Where(r => r.DeviceTypeId == deviceTypeId);

            if (filter.DamageTypeId is int damageTypeId)
                query = query.Where(r => r.DamageTypeId == damageTypeId);

            if (filter.StatusId is int statusId)
                query = query.Where(r => r.MaintenanceRequestStatusId == statusId);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(r => r.User)
                .Include(r => r.Department)
                .Include(r => r.DeviceType)
                .Include(r => r.DamageType)
                .Include(r => r.DeviceCompany)
                .Include(r => r.MaintenanceRequestStatus)
                .AsNoTracking()
                .ToListAsync();

            return (items, total);
        }

        public async Task<List<TechnicianOptionDto>> GetTechniciansAsync(Expression<Func<MaintenanceRequest, bool>> scope)
        {
            var rows = await _context.MaintenanceRequests
                .Where(scope)
                .Select(r => new { r.UserId, r.User.FullName })
                .Distinct()
                .ToListAsync();

            return rows
                .OrderBy(r => r.FullName)
                .Select(r => new TechnicianOptionDto { Id = r.UserId, FullName = r.FullName })
                .ToList();
        }

        public async Task<MaintenanceRequest> AddAsync(MaintenanceRequest request)
        {
            var result = await _context.MaintenanceRequests.AddAsync(request);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(MaintenanceRequest request)
        {
            // الطلب محمَّل ومتتبَّع مع علاقاته — Update() كان سيعلّم الموظف والقسم... كمعدَّلين أيضاً
            if (_context.Entry(request).State == EntityState.Detached)
                _context.MaintenanceRequests.Update(request);

            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(MaintenanceRequest request)
        {
            _context.MaintenanceRequests.Remove(request);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task AddActivityAsync(MaintenanceRequestActivity activity)
        {
            await _context.MaintenanceRequestActivities.AddAsync(activity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<MaintenanceRequestActivity>> GetActivitiesAsync(int requestId) =>
            await _context.MaintenanceRequestActivities
                .Where(a => a.MaintenanceRequestId == requestId)
                .Include(a => a.User)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .AsNoTracking()
                .ToListAsync();

        public async Task<MaintenanceStatsDto> GetStatsAsync(Expression<Func<MaintenanceRequest, bool>> scope)
        {
            var query = _context.MaintenanceRequests.Where(scope);

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var sixMonthsStart = monthStart.AddMonths(-5);

            var stats = new MaintenanceStatsDto
            {
                TotalRequests = await query.CountAsync(),
                RequestsThisMonth = await query.CountAsync(r => r.CreatedAt >= monthStart),

                AverageRepairHours = await query
                    .Where(r => r.StartedAt != null && r.CompletedAt != null)
                    .AverageAsync(r => (double?)EF.Functions.DateDiffMinute(r.StartedAt!.Value, r.CompletedAt!.Value)) / 60.0,

                ByStatus = await query
                    .GroupBy(r => new { r.MaintenanceRequestStatusId, r.MaintenanceRequestStatus.Name, r.MaintenanceRequestStatus.Color })
                    .Select(g => new MaintenanceCountDto { Id = g.Key.MaintenanceRequestStatusId, Name = g.Key.Name, Color = g.Key.Color, Count = g.Count() })
                    .ToListAsync(),

                ByTechnician = await query
                    .GroupBy(r => new { r.UserId, r.User.FullName })
                    .Select(g => new MaintenanceCountDto { Id = g.Key.UserId, Name = g.Key.FullName, Count = g.Count() })
                    .ToListAsync(),

                ByDamageType = await query
                    .GroupBy(r => new { r.DamageTypeId, r.DamageType.Name })
                    .Select(g => new MaintenanceCountDto { Id = g.Key.DamageTypeId, Name = g.Key.Name, Count = g.Count() })
                    .ToListAsync(),

                ByDeviceType = await query
                    .GroupBy(r => new { r.DeviceTypeId, r.DeviceType.Name })
                    .Select(g => new MaintenanceCountDto { Id = g.Key.DeviceTypeId, Name = g.Key.Name, Count = g.Count() })
                    .ToListAsync(),

                ByCompany = await query
                    .GroupBy(r => new { r.DeviceCompanyId, r.DeviceCompany.Name })
                    .Select(g => new MaintenanceCountDto { Id = g.Key.DeviceCompanyId, Name = g.Key.Name, Count = g.Count() })
                    .ToListAsync()
            };

            // آخر 6 أشهر — الأشهر الخالية تظهر بصفر
            var monthly = await query
                .Where(r => r.CreatedAt >= sixMonthsStart)
                .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            for (var i = 0; i < 6; i++)
            {
                var month = sixMonthsStart.AddMonths(i);
                stats.Monthly.Add(new MaintenanceMonthDto
                {
                    Year = month.Year,
                    Month = month.Month,
                    Count = monthly.FirstOrDefault(m => m.Year == month.Year && m.Month == month.Month)?.Count ?? 0
                });
            }

            stats.ByStatus = stats.ByStatus.OrderByDescending(x => x.Count).ToList();
            stats.ByTechnician = stats.ByTechnician.OrderByDescending(x => x.Count).ToList();
            stats.ByDamageType = stats.ByDamageType.OrderByDescending(x => x.Count).ToList();
            stats.ByDeviceType = stats.ByDeviceType.OrderByDescending(x => x.Count).ToList();
            stats.ByCompany = stats.ByCompany.OrderByDescending(x => x.Count).ToList();

            return stats;
        }

        // سجل الطلبات يرتبط بالموظف أيضاً (Restrict)
        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.MaintenanceRequests.AnyAsync(r => r.UserId == userId || r.DeliverySignerId == userId)
            || await _context.MaintenanceRequestActivities.AnyAsync(a => a.UserId == userId);

        public async Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            await _context.MaintenanceRequests.AnyAsync(r => r.DepartmentId == departmentId);
    }
}
