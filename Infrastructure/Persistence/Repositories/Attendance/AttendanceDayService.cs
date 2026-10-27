using Application.Features.Attendance;
using Application.Interfaces;
using Domain.Entities.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendanceDayService : IAttendanceDayService
    {
        private readonly DataContext _context;

        public AttendanceDayService(DataContext context) => _context = context;

        /// <summary>حدّ العرض على نتائج الأيام: اتحاد ما يملكه المستخدم (المنطقة = منطقة الشخص)</summary>
        private IQueryable<AttendanceDay> InScope(AttendanceScope scope)
        {
            var days = _context.AttendanceDays.AsNoTracking();
            if (scope.All) return days;
            if (scope.IsEmpty) return days.Where(d => false);
            var (self, department, branch, area) = (scope.SelfId, scope.DepartmentId, scope.BranchId, scope.AreaId);
            return days.Where(d =>
                (self != null && d.UserId == self)
                || (department != null && d.User.DepartmentId == department)
                || (branch != null && d.User.BranchId == branch)
                || (area != null && (_context.AttendanceProfiles.Where(p => p.UserId == d.UserId).Select(p => p.AreaId).FirstOrDefault()
                    ?? d.User.Branch!.AttendanceAreaId) == area));
        }

        public async Task<(List<DayRow> Items, int Total, DailyCounts Counts)> DailyAsync(AttendanceScope scope, DailyFilter f, int page, int pageSize)
        {
            var date = f.Date.Date;
            var query = InScope(scope).Where(d => d.Date == date);
            if (f.AreaId != null)
                query = query.Where(d => (_context.AttendanceProfiles.Where(p => p.UserId == d.UserId).Select(p => p.AreaId).FirstOrDefault()
                    ?? d.User.Branch!.AttendanceAreaId) == f.AreaId);
            if (f.BranchId != null) query = query.Where(d => d.User.BranchId == f.BranchId);
            if (f.DepartmentId != null) query = query.Where(d => d.User.DepartmentId == f.DepartmentId);
            if (!string.IsNullOrWhiteSpace(f.Query))
            {
                var q = f.Query.Trim();
                var pin = AttendanceRules.PinKey(q);
                query = query.Where(d => d.User.FullName.Contains(q) || d.User.PersonalIdNumber == q
                    || _context.AttendanceProfiles.Any(p => p.UserId == d.UserId && p.AttendanceNumber == pin));
            }

            var grouped = await query.GroupBy(_ => 1).Select(g => new
            {
                Total = g.Count(),
                Present = g.Count(d => d.Status == AttendanceDayStatus.Present),
                Late = g.Count(d => d.LateMinutes > 0),
                Early = g.Count(d => d.EarlyMinutes > 0 || d.ShortMinutes > 0),
                Absent = g.Count(d => d.Status == AttendanceDayStatus.Absent),
                Incomplete = g.Count(d => d.Status == AttendanceDayStatus.Incomplete),
                Excused = g.Count(d => d.Status == AttendanceDayStatus.Excused),
                Pending = g.Count(d => d.Status == AttendanceDayStatus.Pending),
                Vacation = g.Count(d => d.Status == AttendanceDayStatus.Vacation),
                Holiday = g.Count(d => d.Status == AttendanceDayStatus.Holiday),
                Rest = g.Count(d => d.Status == AttendanceDayStatus.Rest)
            }).FirstOrDefaultAsync();
            var counts = grouped == null ? new DailyCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
                : new DailyCounts(grouped.Total, grouped.Present, grouped.Late, grouped.Early, grouped.Absent, grouped.Incomplete,
                    grouped.Excused, grouped.Pending, grouped.Vacation, grouped.Holiday, grouped.Rest);

            query = f.Status switch
            {
                "present" => query.Where(d => d.Status == AttendanceDayStatus.Present),
                "late" => query.Where(d => d.LateMinutes > 0),
                "early" => query.Where(d => d.EarlyMinutes > 0 || d.ShortMinutes > 0),
                "absent" => query.Where(d => d.Status == AttendanceDayStatus.Absent),
                "incomplete" => query.Where(d => d.Status == AttendanceDayStatus.Incomplete),
                "excused" => query.Where(d => d.Status == AttendanceDayStatus.Excused),
                "pending" => query.Where(d => d.Status == AttendanceDayStatus.Pending),
                "vacation" => query.Where(d => d.Status == AttendanceDayStatus.Vacation),
                "holiday" => query.Where(d => d.Status == AttendanceDayStatus.Holiday),
                "rest" => query.Where(d => d.Status == AttendanceDayStatus.Rest),
                _ => query
            };

            var total = await query.CountAsync();
            var rows = await query
                .OrderBy(d => d.Status == AttendanceDayStatus.Absent ? 0 : d.Status == AttendanceDayStatus.Incomplete ? 1 : d.LateMinutes > 0 ? 2 : 3)
                .ThenBy(d => d.User.FullName)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(d => new
                {
                    Day = d,
                    Parts = d.Parts.OrderBy(p => p.Order).ToList(),
                    d.User.FullName,
                    d.User.Kind,
                    Number = _context.AttendanceProfiles.Where(p => p.UserId == d.UserId).Select(p => p.AttendanceNumber).FirstOrDefault(),
                    Branch = d.User.Branch != null ? d.User.Branch.Name : null,
                    Department = d.User.Department != null ? d.User.Department.Name : null,
                    Shift = d.Shift != null ? d.Shift.Name : null
                }).ToListAsync();
            foreach (var r in rows) r.Day.Parts = r.Parts;
            return (rows.Select(r => new DayRow(r.Day, r.FullName, r.Kind, r.Number ?? string.Empty, r.Branch, r.Department, r.Shift)).ToList(),
                total, counts);
        }

        public async Task<bool> PersonInScopeAsync(AttendanceScope scope, int userId)
        {
            if (scope.All) return await _context.Users.AnyAsync(u => u.Id == userId);
            if (scope.IsEmpty) return false;
            var (self, department, branch, area) = (scope.SelfId, scope.DepartmentId, scope.BranchId, scope.AreaId);
            return await _context.Users.AsNoTracking().AnyAsync(u => u.Id == userId && (
                (self != null && u.Id == self)
                || (department != null && u.DepartmentId == department)
                || (branch != null && u.BranchId == branch)
                || (area != null && (_context.AttendanceProfiles.Where(p => p.UserId == u.Id).Select(p => p.AreaId).FirstOrDefault()
                    ?? u.Branch!.AttendanceAreaId) == area)));
        }

        public Task<List<AttendanceDay>> DaysAsync(int userId, DateTime from, DateTime to) =>
            _context.AttendanceDays.AsNoTracking().Include(d => d.Parts)
                .Where(d => d.UserId == userId && d.Date >= from.Date && d.Date <= to.Date)
                .OrderBy(d => d.Date).ToListAsync();

        public async Task<List<SheetPunch>> PunchesAsync(int userId, DateTime from, DateTime to)
        {
            var start = from.Date;
            var end = to.Date.AddDays(1);
            var corrections = await _context.AttendanceCorrections.AsNoTracking()
                .Where(c => c.UserId == userId && c.CancelledAt == null && c.FromDate <= to.Date.AddDays(1) && c.ToDate >= start.AddDays(-1))
                .Where(c => c.Kind == AttendanceCorrectionKind.AddPunch || c.Kind == AttendanceCorrectionKind.IgnorePunch)
                .Select(c => new { c.Id, c.Kind, c.PunchId, c.PunchTime }).ToListAsync();
            var ignored = corrections.Where(c => c.Kind == AttendanceCorrectionKind.IgnorePunch && c.PunchId != null)
                .ToDictionary(c => c.PunchId!.Value, c => c.Id);

            var raw = await _context.AttendancePunches.AsNoTracking()
                .Where(p => p.UserId == userId && p.PunchTime >= start && p.PunchTime < end)
                .Select(p => new { p.Id, p.PunchTime, p.Device.Name, p.VerifyType }).ToListAsync();
            var list = raw.Select(p => new SheetPunch(p.Id, ignored.TryGetValue(p.Id, out var cid) ? cid : null, p.PunchTime, p.Name,
                p.VerifyType, false, ignored.ContainsKey(p.Id))).ToList();
            list.AddRange(corrections
                .Where(c => c.Kind == AttendanceCorrectionKind.AddPunch && c.PunchTime >= start && c.PunchTime < end)
                .Select(c => new SheetPunch(null, c.Id, c.PunchTime!.Value, null, 0, true, false)));
            return list.OrderBy(p => p.Time).ToList();
        }

        public Task<Dictionary<int, string>> ShiftNamesAsync() =>
            _context.AttendanceShifts.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);
    }
}
