using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DeviceMaintenanceService : IDeviceMaintenanceService
    {
        private readonly DataContext _context;

        public DeviceMaintenanceService(DataContext context)
        {
            _context = context;
        }

        private IQueryable<DeviceMaintenance> WithDetails() => _context.DeviceMaintenances
            .Include(d => d.DeviceType)
            .Include(d => d.DeviceCompany);

        public async Task<DeviceMaintenance?> GetByIdAsync(int id) =>
            await WithDetails().FirstOrDefaultAsync(d => d.Id == id);

        // مطابقة تامة على الفهرس الفريد (الترتيب في SQL Server لا يميّز حالة الأحرف)
        public async Task<DeviceMaintenance?> GetBySerialAsync(string serialNumber) =>
            await WithDetails().AsNoTracking().FirstOrDefaultAsync(d => d.SerialNumber == serialNumber);

        public async Task<(List<DeviceMaintenance> Items, int TotalCount)> SearchAsync(
            DeviceMaintenanceFilterDto filter, int page, int pageSize)
        {
            var query = _context.DeviceMaintenances.AsQueryable();

            // "يبدأ بـ" تُترجم إلى LIKE 'x%' فتستخدم الفهرس (Index Seek)
            if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            {
                var serial = filter.SerialNumber.Trim();
                query = query.Where(d => d.SerialNumber.StartsWith(serial));
            }

            if (!string.IsNullOrWhiteSpace(filter.Model))
            {
                var model = filter.Model.Trim();
                query = query.Where(d => d.Model.StartsWith(model));
            }

            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                var name = filter.Name.Trim();
                query = query.Where(d => d.Name.Contains(name));
            }

            if (filter.DeviceTypeId is int deviceTypeId)
                query = query.Where(d => d.DeviceTypeId == deviceTypeId);

            if (filter.DeviceCompanyId is int companyId)
                query = query.Where(d => d.DeviceCompanyId == companyId);

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(d => d.SerialNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(d => d.DeviceType)
                .Include(d => d.DeviceCompany)
                .AsNoTracking()
                .ToListAsync();

            return (items, total);
        }

        public async Task<DeviceMaintenance> AddAsync(DeviceMaintenance device)
        {
            var result = await _context.DeviceMaintenances.AddAsync(device);
            await SaveAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(DeviceMaintenance device)
        {
            // الجهاز محمَّل ومتتبَّع مع النوع والشركة — Update() كان سيعلّمهما كمعدَّلين أيضاً
            if (_context.Entry(device).State == EntityState.Detached)
                _context.DeviceMaintenances.Update(device);

            return await SaveAsync() > 0;
        }

        /// <summary>
        /// الفحص المسبق (ExistsBySerialAsync) لا يمنع إضافتين في اللحظة نفسها — الفهرس الفريد يمنعها،
        /// فنحوّل خطأه (2601/2627) إلى رسالة واضحة بدل خطأ خادم 500
        /// </summary>
        private async Task<int> SaveAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql
                                               && sql.Message.Contains("IX_DeviceMaintenances_SerialNumber"))
            {
                throw new InvalidOperationException("يوجد جهاز بنفس الرقم التسلسلي مسبقاً — اختره بدل إضافته من جديد");
            }
        }

        public async Task<bool> DeleteAsync(DeviceMaintenance device)
        {
            _context.DeviceMaintenances.Remove(device);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsAsync(int id) =>
            await _context.DeviceMaintenances.AnyAsync(d => d.Id == id);

        public async Task<bool> ExistsBySerialAsync(string serialNumber, int? excludeId = null) =>
            await _context.DeviceMaintenances.AnyAsync(d => d.SerialNumber == serialNumber && d.Id != excludeId);

        public async Task<bool> HasRequestsAsync(int id) =>
            await _context.MaintenanceRequests.AnyAsync(r => r.DeviceMaintenanceId == id);
    }
}
