using Application.Interfaces;
using Domain.Entities.Attendance;
using Infrastructure.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendanceDeviceService : IAttendanceDeviceService
    {
        private readonly DataContext _context;
        private readonly AttendanceDeviceCache _cache;

        public AttendanceDeviceService(DataContext context, AttendanceDeviceCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private IQueryable<DeviceRow> Rows(IQueryable<AttendanceDevice> devices) =>
            devices.Select(d => new DeviceRow(d, d.Area != null ? d.Area.Name : null, d.ApprovedByUser != null ? d.ApprovedByUser.FullName : null,
                _context.AttendanceDeviceCommands.Count(c => c.DeviceId == d.Id && (c.State == AttendanceCommandState.Queued || c.State == AttendanceCommandState.Sent)),
                _context.AttendanceDeviceUsers.Count(u => u.DeviceId == d.Id && u.Managed)));

        public async Task<List<DeviceRow>> GetAllAsync(AreaLimit limit, AttendanceDeviceState? state, int? areaId)
        {
            var query = _context.AttendanceDevices.AsNoTracking().AsQueryable();
            if (!limit.All)
            {
                var own = limit.AreaId;
                query = query.Where(d => own != null && d.AreaId == own && d.State == AttendanceDeviceState.Approved);
            }
            if (state != null) query = query.Where(d => d.State == state);
            if (areaId != null) query = query.Where(d => d.AreaId == areaId);
            return await Rows(query.OrderByDescending(d => d.LastSeenAt)).ToListAsync();
        }

        public Task<DeviceRow?> GetRowAsync(int id) =>
            Rows(_context.AttendanceDevices.AsNoTracking().Where(d => d.Id == id))
                .FirstOrDefaultAsync();

        public Task<AttendanceDevice?> GetByIdAsync(int id) =>
            _context.AttendanceDevices.Include(d => d.Area).FirstOrDefaultAsync(d => d.Id == id);

        public async Task SaveAsync(AttendanceDevice device, params AttendanceDeviceLog[] logs)
        {
            _context.AttendanceDeviceLogs.AddRange(logs);
            await DeviceInventorySave.SaveAsync(_context, "يوجد جهاز بنفس الرقم التسلسلي");
            _cache.Invalidate(device.SerialNumber);
        }

        public async Task EnqueueAsync(IReadOnlyCollection<AttendanceDeviceCommand> commands)
        {
            if (commands.Count == 0) return;
            _context.AttendanceDeviceCommands.AddRange(commands);
            await _context.SaveChangesAsync();
            foreach (var deviceId in commands.Select(c => c.DeviceId).Distinct()) _cache.CommandsQueued(deviceId);
        }

        public Task CancelQueuedAsync(int deviceId) =>
            _context.AttendanceDeviceCommands
                .Where(c => c.DeviceId == deviceId && c.State == AttendanceCommandState.Queued)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.State, AttendanceCommandState.Expired).SetProperty(c => c.CompletedAt, DateTime.UtcNow));

        public async Task RequestFullResyncAsync(int deviceId)
        {
            await _context.AttendanceDeviceUsers.Where(u => u.DeviceId == deviceId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.SyncedHash, (string?)null).SetProperty(u => u.BiometricHash, (string?)null));
            await MarkNeedsSyncAsync(deviceId);
        }

        public Task MarkNeedsSyncAsync(int deviceId) =>
            _context.AttendanceDevices.Where(d => d.Id == deviceId).ExecuteUpdateAsync(s => s.SetProperty(d => d.NeedsSync, true));

        public async Task<(List<AttendanceDeviceCommand> Items, int Total)> CommandsAsync(int deviceId, int page, int pageSize)
        {
            var query = _context.AttendanceDeviceCommands.AsNoTracking().Where(c => c.DeviceId == deviceId);
            var total = await query.CountAsync();
            // النص الكامل (قوالب وصور) لا يُحمَّل في القائمة: الوصف يكفي
            var items = await query.OrderByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new AttendanceDeviceCommand
                {
                    Id = c.Id, DeviceId = c.DeviceId, Kind = c.Kind, Summary = c.Summary, UserId = c.UserId, State = c.State,
                    ReturnCode = c.ReturnCode, CreatedByUserId = c.CreatedByUserId, CreatedByUser = c.CreatedByUser,
                    CreatedAt = c.CreatedAt, SentAt = c.SentAt, CompletedAt = c.CompletedAt
                }).ToListAsync();
            return (items, total);
        }

        public Task<List<AttendanceDeviceLog>> LogAsync(int deviceId, int take) =>
            _context.AttendanceDeviceLogs.AsNoTracking().Include(l => l.User)
                .Where(l => l.DeviceId == deviceId).OrderByDescending(l => l.Id).Take(take).ToListAsync();

        public Task<List<AttendanceDeviceUser>> DeviceUsersAsync(int deviceId) =>
            _context.AttendanceDeviceUsers.AsNoTracking().Include(u => u.User)
                .Where(u => u.DeviceId == deviceId).OrderBy(u => u.Pin).ToListAsync();
    }
}
