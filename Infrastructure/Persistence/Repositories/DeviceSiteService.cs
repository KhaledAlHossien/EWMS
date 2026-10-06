using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DeviceSiteService : IDeviceSiteService
    {
        private readonly DataContext _context;

        public DeviceSiteService(DataContext context)
        {
            _context = context;
        }

        public async Task<DeviceSite?> GetByIdAsync(int id) =>
            await _context.DeviceSites.Include(ds => ds.Device).Include(ds => ds.Site).FirstOrDefaultAsync(ds => ds.Id == id);

        public async Task<InstallationRow?> GetRowAsync(int id) =>
            (await RowsAsync(_context.DeviceSites.Where(ds => ds.Id == id))).FirstOrDefault();

        private IQueryable<DeviceSite> Filtered(InstallationFilterDto f)
        {
            var q = _context.DeviceSites.AsQueryable();
            if (f.SiteId is int siteId) q = q.Where(ds => ds.SiteId == siteId);
            if (f.DeviceId is int deviceId) q = q.Where(ds => ds.DeviceId == deviceId);
            if (f.Status is int status) q = q.Where(ds => ds.Status == (InstallationStatus)status);
            if (!string.IsNullOrWhiteSpace(f.GovernorateCode))
            {
                var code = f.GovernorateCode.Trim();
                q = q.Where(ds => ds.Site.GovernorateCode == code);
            }
            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                var s = f.Q.Trim();
                q = q.Where(ds => ds.Ip.Contains(s) || ds.SN.Contains(s) || ds.UserName.Contains(s) || ds.InstallLocation.Contains(s)
                    || ds.Note.Contains(s) || ds.MacAddress.Contains(s) || ds.Device.Name.Contains(s) || ds.Device.Model.Contains(s)
                    || ds.Site.Name.Contains(s));
            }
            return q;
        }

        /// <summary>مع الجهاز والموقع وعلامة تكرار الـ IP في الموقع، بترتيب ثابت</summary>
        private async Task<List<InstallationRow>> RowsAsync(IQueryable<DeviceSite> query, int skip = 0, int take = int.MaxValue)
        {
            var rows = await query.AsNoTracking()
                .Include(ds => ds.Device).Include(ds => ds.Site)
                .OrderBy(ds => ds.Site.Name).ThenBy(ds => ds.Device.Name).ThenBy(ds => ds.Ip).ThenBy(ds => ds.Id)
                .Skip(skip).Take(take)
                .Select(ds => new { Installation = ds, Duplicate = _context.DeviceSites.Any(o => o.SiteId == ds.SiteId && o.Ip == ds.Ip && o.Id != ds.Id) })
                .ToListAsync();
            return rows.Select(r => new InstallationRow(r.Installation, r.Duplicate)).ToList();
        }

        public async Task<(List<InstallationRow> Items, int TotalCount)> SearchAsync(InstallationFilterDto filter, int page, int pageSize)
        {
            var query = Filtered(filter);
            var total = await query.CountAsync();
            return (await RowsAsync(query, (page - 1) * pageSize, pageSize), total);
        }

        public Task<List<InstallationRow>> ExportAsync(InstallationFilterDto filter, int max) => RowsAsync(Filtered(filter), 0, max);

        public async Task<List<DeviceSite>> IpInUseAsync(int siteId, string ip, int? excludeId) =>
            await _context.DeviceSites.AsNoTracking().Include(ds => ds.Device)
                .Where(ds => ds.SiteId == siteId && ds.Ip == ip && ds.Id != excludeId)
                .ToListAsync();

        public async Task<DeviceSite> AddAsync(DeviceSite deviceSite)
        {
            await _context.DeviceSites.AddAsync(deviceSite);
            await SaveAsync();
            return deviceSite;
        }

        public async Task UpdateAsync(DeviceSite deviceSite, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(deviceSite).Property(ds => ds.RowVersion).OriginalValue = rowVersion;
            await SaveAsync();
        }

        public async Task DeleteAsync(DeviceSite deviceSite)
        {
            _context.DeviceSites.Remove(deviceSite);
            await SaveAsync();
        }

        public async Task ImportAsync(List<Device> newDevices, List<DeviceSite> installations)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.Devices.AddRange(newDevices);
            _context.DeviceSites.AddRange(installations);
            await DeviceInventorySave.SaveAsync(_context, "تعارض في أسماء الأجهزة أثناء الاستيراد — أعد المحاولة");
            await transaction.CommitAsync();
        }

        private Task SaveAsync() => DeviceInventorySave.SaveAsync(_context, "تعارض في بيانات التركيب");
    }

    public class DeviceInventoryLogService : IDeviceInventoryLogService
    {
        private readonly DataContext _context;

        public DeviceInventoryLogService(DataContext context)
        {
            _context = context;
        }

        public async Task AddAsync(DeviceInventoryLog log)
        {
            _context.DeviceInventoryLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<DeviceInventoryLog> logs)
        {
            _context.DeviceInventoryLogs.AddRange(logs);
            await _context.SaveChangesAsync();
        }

        public async Task<(List<DeviceInventoryLog> Items, int TotalCount)> GetAsync(DeviceInventoryEntity type, int entityId, int page, int pageSize)
        {
            var query = _context.DeviceInventoryLogs.AsNoTracking().Where(l => l.EntityType == type && l.EntityId == entityId);
            var total = await query.CountAsync();
            var items = await query.Include(l => l.User)
                .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.DeviceInventoryLogs.AnyAsync(l => l.UserId == userId);
    }
}
