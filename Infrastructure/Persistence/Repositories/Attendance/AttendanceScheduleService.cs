using Application.Interfaces;
using Domain.Entities.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendanceScheduleService : IAttendanceScheduleService
    {
        private const int OptionLimit = 50;
        private readonly DataContext _context;

        public AttendanceScheduleService(DataContext context) => _context = context;

        public Task<List<AttendanceSchedule>> GetSchedulesAsync(AttendanceScheduleTarget? type, int? targetId)
        {
            var query = _context.AttendanceSchedules.AsNoTracking()
                .Include(s => s.Shift).Include(s => s.CreatedByUser).Include(s => s.UpdatedByUser).AsQueryable();
            if (type != null) query = query.Where(s => s.TargetType == type);
            if (targetId != null) query = query.Where(s => s.TargetId == targetId);
            return query.OrderBy(s => s.TargetType).ThenBy(s => s.TargetId).ThenByDescending(s => s.FromDate).ToListAsync();
        }

        public Task<AttendanceSchedule?> GetScheduleAsync(int id) =>
            _context.AttendanceSchedules.FirstOrDefaultAsync(s => s.Id == id);

        public Task<AttendanceSchedule?> FindOverlapAsync(AttendanceScheduleTarget type, int targetId, DateTime from, DateTime? to, int? excludeId)
        {
            var end = to ?? DateTime.MaxValue.Date;
            return _context.AttendanceSchedules.AsNoTracking().FirstOrDefaultAsync(s =>
                s.TargetType == type && s.TargetId == targetId && s.Id != excludeId
                && s.FromDate <= end && (s.ToDate == null || s.ToDate >= from));
        }

        public async Task SaveScheduleAsync(AttendanceSchedule schedule, byte[]? rowVersion)
        {
            if (schedule.Id == 0) _context.AttendanceSchedules.Add(schedule);
            else if (rowVersion is { Length: > 0 })
                _context.Entry(schedule).Property(s => s.RowVersion).OriginalValue = rowVersion;
            await DeviceInventorySave.SaveAsync(_context, "يوجد إسناد مطابق");
        }

        public async Task DeleteScheduleAsync(AttendanceSchedule schedule)
        {
            _context.AttendanceSchedules.Remove(schedule);
            await DeviceInventorySave.SaveAsync(_context, "يوجد إسناد مطابق");
        }

        public Task<List<AttendanceTemporarySchedule>> GetTemporariesAsync(AttendanceScheduleTarget? type, int? targetId, DateTime since)
        {
            var query = _context.AttendanceTemporarySchedules.AsNoTracking()
                .Include(t => t.Timetable).Include(t => t.CreatedByUser)
                .Where(t => t.ToDate >= since);
            if (type != null) query = query.Where(t => t.TargetType == type);
            if (targetId != null) query = query.Where(t => t.TargetId == targetId);
            return query.OrderBy(t => t.FromDate).ToListAsync();
        }

        public Task<AttendanceTemporarySchedule?> GetTemporaryAsync(int id) =>
            _context.AttendanceTemporarySchedules.FirstOrDefaultAsync(t => t.Id == id);

        public Task<AttendanceTemporarySchedule?> FindTemporaryOverlapAsync(AttendanceScheduleTarget type, int targetId, DateTime from, DateTime to) =>
            _context.AttendanceTemporarySchedules.AsNoTracking().FirstOrDefaultAsync(t =>
                t.TargetType == type && t.TargetId == targetId && t.FromDate <= to && t.ToDate >= from);

        public async Task AddTemporaryAsync(AttendanceTemporarySchedule temporary)
        {
            _context.AttendanceTemporarySchedules.Add(temporary);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteTemporaryAsync(AttendanceTemporarySchedule temporary)
        {
            _context.AttendanceTemporarySchedules.Remove(temporary);
            await _context.SaveChangesAsync();
        }

        public async Task<Dictionary<(AttendanceScheduleTarget Type, int Id), ScheduleTargetInfo>> TargetsAsync(IEnumerable<(AttendanceScheduleTarget Type, int Id)> keys)
        {
            var list = keys.Distinct().ToList();
            var result = new Dictionary<(AttendanceScheduleTarget Type, int Id), ScheduleTargetInfo>();
            foreach (var group in list.GroupBy(k => k.Type))
            {
                var ids = group.Select(k => k.Id).ToList();
                foreach (var info in await QueryOf(group.Key, null, null).Where(i => ids.Contains(i.Id)).ToListAsync())
                    result[(group.Key, info.Id)] = info;
                foreach (var id in ids.Where(id => !result.ContainsKey((group.Key, id))))
                    result[(group.Key, id)] = new ScheduleTargetInfo(group.Key, id, "جهة محذوفة", string.Empty, null, false);
            }
            return result;
        }

        public async Task<List<ScheduleTargetInfo>> TargetOptionsAsync(AttendanceScheduleTarget type, string? query, AreaLimit limit)
        {
            if (limit.IsNothing) return [];
            var q = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
            return await QueryOf(type, q, limit.All ? null : limit.AreaId).OrderBy(i => i.Name).Take(OptionLimit).ToListAsync();
        }

        /// <summary>جهات نوع واحد بأسمائها ومناطقها؛ area = حدّ منطقة، q = بحث</summary>
        private IQueryable<ScheduleTargetInfo> QueryOf(AttendanceScheduleTarget type, string? q, int? area)
        {
            switch (type)
            {
                case AttendanceScheduleTarget.Area:
                    return _context.AttendanceAreas.AsNoTracking()
                        .Where(a => (area == null || a.Id == area) && (q == null || a.Name.Contains(q)))
                        .Select(a => new ScheduleTargetInfo(type, a.Id, a.Name, string.Empty, a.Id, true));
                case AttendanceScheduleTarget.Branch:
                    return _context.Branches.AsNoTracking()
                        .Where(b => (area == null || b.AttendanceAreaId == area) && (q == null || b.Name.Contains(q)))
                        .Select(b => new ScheduleTargetInfo(type, b.Id, b.Name,
                            b.AttendanceArea != null ? b.AttendanceArea.Name : "بلا منطقة", b.AttendanceAreaId, true));
                case AttendanceScheduleTarget.Department:
                    return _context.Departments.AsNoTracking()
                        .Where(d => (area == null || d.Branch.AttendanceAreaId == area) && (q == null || d.Name.Contains(q)))
                        .Select(d => new ScheduleTargetInfo(type, d.Id, d.Name, d.Branch.Name, d.Branch.AttendanceAreaId, true));
                case AttendanceScheduleTarget.Office:
                    return _context.Offices.AsNoTracking()
                        .Where(o => (area == null || o.Department.Branch.AttendanceAreaId == area) && (q == null || o.Name.Contains(q)))
                        .Select(o => new ScheduleTargetInfo(type, o.Id, o.Name, o.Department.Name + " — " + o.Department.Branch.Name,
                            o.Department.Branch.AttendanceAreaId, true));
                default:
                    var pin = q?.TrimStart('0');
                    return _context.AttendanceProfiles.AsNoTracking()
                        .Where(p => area == null || (p.AreaId ?? p.User.Branch!.AttendanceAreaId) == area)
                        .Where(p => q == null || p.User.FullName.Contains(q) || p.AttendanceNumber == pin || p.User.PersonalIdNumber == q)
                        .Select(p => new ScheduleTargetInfo(type, p.UserId, p.User.FullName,
                            (p.AttendanceNumber != null ? "رقم " + p.AttendanceNumber : "بلا رقم بصمة")
                                + (p.User.Department != null ? " · " + p.User.Department.Name : p.AreaId != null ? " · عامل مياومة" : ""),
                            p.AreaId ?? p.User.Branch!.AttendanceAreaId, true));
            }
        }

        public Task<List<int>> UsersOfTargetAsync(AttendanceScheduleTarget type, int targetId)
        {
            var profiles = _context.AttendanceProfiles.AsNoTracking().Where(p => p.AttendanceNumber != null);
            profiles = type switch
            {
                AttendanceScheduleTarget.Person => profiles.Where(p => p.UserId == targetId),
                AttendanceScheduleTarget.Office => profiles.Where(p => p.User.OfficeId == targetId),
                AttendanceScheduleTarget.Department => profiles.Where(p => p.User.DepartmentId == targetId),
                AttendanceScheduleTarget.Branch => profiles.Where(p => p.User.BranchId == targetId),
                _ => profiles.Where(p => (p.AreaId ?? p.User.Branch!.AttendanceAreaId) == targetId)
            };
            return profiles.Select(p => p.UserId).ToListAsync();
        }
    }
}
