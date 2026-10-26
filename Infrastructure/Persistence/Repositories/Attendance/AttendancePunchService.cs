using Application.Features.Attendance;
using Application.Interfaces;
using Domain.Entities.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendancePunchService : IAttendancePunchService
    {
        private readonly DataContext _context;

        public AttendancePunchService(DataContext context) => _context = context;

        /// <summary>حدّ العرض: اتحاد ما يملكه المستخدم (المنطقة تشمل أشخاصها وبصمات أجهزتها)</summary>
        private IQueryable<AttendancePunch> InScope(AttendanceScope scope)
        {
            var punches = _context.AttendancePunches.AsNoTracking();
            if (scope.All) return punches;
            if (scope.IsEmpty) return punches.Where(p => false);
            var (self, department, branch, area) = (scope.SelfId, scope.DepartmentId, scope.BranchId, scope.AreaId);
            return punches.Where(p =>
                (self != null && p.UserId == self)
                || (department != null && p.User != null && p.User.DepartmentId == department)
                || (branch != null && p.User != null && p.User.BranchId == branch)
                || (area != null && (p.Device.AreaId == area
                    || (p.User != null && (_context.AttendanceProfiles.Where(pr => pr.UserId == p.UserId).Select(pr => pr.AreaId).FirstOrDefault()
                        ?? p.User.Branch!.AttendanceAreaId) == area))));
        }

        public async Task<(List<PunchRow> Items, int Total)> SearchAsync(AttendanceScope scope, PunchFilter f, int page, int pageSize)
        {
            var query = InScope(scope).Where(p => p.PunchTime >= f.From && p.PunchTime < f.To);
            if (f.DeviceId != null) query = query.Where(p => p.DeviceId == f.DeviceId);
            if (f.AreaId != null) query = query.Where(p => p.Device.AreaId == f.AreaId);
            if (f.UserId != null) query = query.Where(p => p.UserId == f.UserId);
            if (f.UnknownOnly) query = query.Where(p => p.UserId == null);
            if (!string.IsNullOrWhiteSpace(f.Query))
            {
                var q = f.Query.Trim();
                var pin = AttendanceRules.PinKey(q);
                var id = q.ToUpperInvariant();
                query = query.Where(p => p.Pin == q || p.Pin == pin
                    || (p.User != null && (p.User.FullName.Contains(q) || p.User.PersonalIdNumber == id)));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.PunchTime).ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new PunchRow(p,
                    p.User != null ? p.User.FullName : null,
                    p.User != null ? p.User.Kind : null,
                    p.User != null && p.User.Department != null ? p.User.Department.Name : null,
                    p.Device.Name,
                    p.Device.Area != null ? p.Device.Area.Name : null,
                    _context.AttendancePunchPhotos.Any(ph => ph.DeviceSerial == p.DeviceSerial && ph.Pin == p.Pin && ph.PunchTime == p.PunchTime)))
                .ToListAsync();
            return (items, total);
        }

        public async Task<List<UnknownPinRow>> UnknownPinsAsync(AreaLimit limit, int? areaId)
        {
            var query = _context.AttendancePunches.AsNoTracking().Where(p => p.UserId == null);
            if (!limit.All)
            {
                var own = limit.AreaId;
                query = query.Where(p => own != null && p.Device.AreaId == own);
            }
            if (areaId != null) query = query.Where(p => p.Device.AreaId == areaId);

            var groups = await query.GroupBy(p => p.Pin)
                .Select(g => new { Pin = g.Key, Count = g.Count(), First = g.Min(p => p.PunchTime), Last = g.Max(p => p.PunchTime) })
                .OrderByDescending(g => g.Last).Take(500).ToListAsync();
            var pins = groups.Select(g => g.Pin).ToList();
            var devices = await query.Where(p => pins.Contains(p.Pin))
                .Select(p => new { p.Pin, p.Device.Name }).Distinct().ToListAsync();
            return groups.Select(g => new UnknownPinRow(g.Pin, g.Count, g.First, g.Last,
                devices.Where(d => d.Pin == g.Pin).Select(d => d.Name).OrderBy(n => n).ToList())).ToList();
        }

        public async Task<AttendancePunchPhoto?> PhotoForAsync(long punchId)
        {
            var punch = await _context.AttendancePunches.AsNoTracking().FirstOrDefaultAsync(p => p.Id == punchId);
            if (punch == null) return null;
            return await _context.AttendancePunchPhotos.AsNoTracking().FirstOrDefaultAsync(ph =>
                ph.DeviceSerial == punch.DeviceSerial && ph.Pin == punch.Pin && ph.PunchTime == punch.PunchTime);
        }

        public Task<byte[]?> PhotoDataAsync(long photoId) =>
            _context.AttendancePunchPhotoContents.AsNoTracking().Where(c => c.PhotoId == photoId).Select(c => (byte[]?)c.Data).FirstOrDefaultAsync();

        /// <summary>هل البصمة ضمن حد العرض (للصورة)</summary>
        public Task<bool> InScopeAsync(AttendanceScope scope, long punchId) => InScope(scope).AnyAsync(p => p.Id == punchId);
    }
}
