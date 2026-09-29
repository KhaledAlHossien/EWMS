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
            .Include(r => r.MaintenanceRequestStatus);

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

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.MaintenanceRequests.AnyAsync(r => r.UserId == userId);

        public async Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            await _context.MaintenanceRequests.AnyAsync(r => r.DepartmentId == departmentId);
    }
}
