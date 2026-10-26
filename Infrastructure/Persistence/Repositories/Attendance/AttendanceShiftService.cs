using Application.Interfaces;
using Domain.Entities.Attendance;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AttendanceShiftService : IAttendanceShiftService
    {
        private readonly DataContext _context;

        public AttendanceShiftService(DataContext context) => _context = context;

        public Task<List<AttendanceTimetable>> GetTimetablesAsync() =>
            _context.AttendanceTimetables.AsNoTracking().ToListAsync();

        public Task<AttendanceTimetable?> GetTimetableAsync(int id) =>
            _context.AttendanceTimetables.FirstOrDefaultAsync(t => t.Id == id);

        public Task<bool> TimetableNameExistsAsync(string name, int? excludeId = null) =>
            _context.AttendanceTimetables.AnyAsync(t => t.Name == name && t.Id != excludeId);

        public async Task SaveTimetableAsync(AttendanceTimetable timetable, byte[]? rowVersion)
        {
            if (timetable.Id == 0) _context.AttendanceTimetables.Add(timetable);
            else if (rowVersion is { Length: > 0 })
                _context.Entry(timetable).Property(t => t.RowVersion).OriginalValue = rowVersion;
            await DeviceInventorySave.SaveAsync(_context, "يوجد جدول زمني بنفس الاسم");
        }

        public async Task<Dictionary<int, int>> TimetableUsageAsync()
        {
            var inShifts = await _context.AttendanceShiftDays.AsNoTracking()
                .GroupBy(d => d.TimetableId).Select(g => new { Id = g.Key, Count = g.Select(d => d.ShiftId).Distinct().Count() })
                .ToListAsync();
            var inTemporary = await _context.AttendanceTemporarySchedules.AsNoTracking()
                .Where(t => t.TimetableId != null)
                .GroupBy(t => t.TimetableId!.Value).Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync();
            return inShifts.Concat(inTemporary).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        }

        public async Task DeleteTimetableAsync(AttendanceTimetable timetable)
        {
            _context.AttendanceTimetables.Remove(timetable);
            await DeviceInventorySave.SaveAsync(_context, "يوجد جدول زمني بنفس الاسم");
        }

        public Task<List<AttendanceShift>> GetShiftsAsync() =>
            _context.AttendanceShifts.AsNoTracking().Include(s => s.Days).ThenInclude(d => d.Timetable).AsSplitQuery().ToListAsync();

        public Task<AttendanceShift?> GetShiftAsync(int id) =>
            _context.AttendanceShifts.Include(s => s.Days).FirstOrDefaultAsync(s => s.Id == id);

        public Task<bool> ShiftNameExistsAsync(string name, int? excludeId = null) =>
            _context.AttendanceShifts.AnyAsync(s => s.Name == name && s.Id != excludeId);

        public async Task SaveShiftAsync(AttendanceShift shift, byte[]? rowVersion, IReadOnlyCollection<(int DayIndex, int TimetableId)> days)
        {
            if (shift.Id == 0) _context.AttendanceShifts.Add(shift);
            else if (rowVersion is { Length: > 0 })
                _context.Entry(shift).Property(s => s.RowVersion).OriginalValue = rowVersion;

            // الأيام كما أُرسلت بالضبط: ما لم يعد موجوداً يُحذف، والجديد يُضاف
            var wanted = days.ToHashSet();
            foreach (var day in shift.Days.Where(d => !wanted.Contains((d.DayIndex, d.TimetableId))).ToList())
                shift.Days.Remove(day);
            var have = shift.Days.Select(d => (d.DayIndex, d.TimetableId)).ToHashSet();
            foreach (var (dayIndex, timetableId) in wanted.Where(w => !have.Contains(w)))
                shift.Days.Add(new AttendanceShiftDay { DayIndex = dayIndex, TimetableId = timetableId });

            // تغيير الأيام وحده يجب أن يمسّ صف الوردية كي يُكشف التعديل المتزامن
            if (shift.Id != 0) _context.Entry(shift).Property(s => s.UpdatedAt).IsModified = true;
            await DeviceInventorySave.SaveAsync(_context, "توجد وردية بنفس الاسم");
        }

        public async Task<Dictionary<int, int>> ShiftUsageAsync() =>
            await _context.AttendanceSchedules.AsNoTracking()
                .GroupBy(s => s.ShiftId).Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

        public async Task DeleteShiftAsync(AttendanceShift shift)
        {
            _context.AttendanceShifts.Remove(shift);
            await DeviceInventorySave.SaveAsync(_context, "توجد وردية بنفس الاسم");
        }
    }

    /// <summary>
    /// طابور إعادة الحساب. اليوم = التوقيت المحلي للأجهزة؛ ما بعده لا يُحسب، وأقصى مدة للخلف 400 يوم
    /// (إسناد يبدأ قبل ذلك لا يعيد حساب سنين).
    /// </summary>
    public class AttendanceRecalcQueue : IAttendanceRecalcQueue
    {
        private const int MaxDaysBack = 400;
        private readonly DataContext _context;
        private readonly IAttendanceSettings _settings;

        public AttendanceRecalcQueue(DataContext context, IAttendanceSettings settings)
        {
            _context = context;
            _settings = settings;
        }

        private (DateTime From, DateTime To)? Clamp(DateTime from, DateTime to)
        {
            var today = DateTime.UtcNow.AddHours(_settings.TimeZoneHours).Date;
            var start = from.Date < today.AddDays(-MaxDaysBack) ? today.AddDays(-MaxDaysBack) : from.Date;
            var end = to.Date > today ? today : to.Date;
            return start > end ? null : (start, end);
        }

        public Task EnqueueAsync(int? userId, DateTime from, DateTime to, string reason) =>
            AddAsync([(userId, from, to)], reason);

        public Task EnqueueManyAsync(IEnumerable<(int UserId, DateTime From, DateTime To)> items, string reason) =>
            AddAsync(items.Select(i => ((int?)i.UserId, i.From, i.To)), reason);

        private async Task AddAsync(IEnumerable<(int? UserId, DateTime From, DateTime To)> items, string reason)
        {
            var now = DateTime.UtcNow;
            var rows = items
                .Select(i => (i.UserId, Range: Clamp(i.From, i.To)))
                .Where(i => i.Range != null)
                .Select(i => new AttendanceRecalcRequest
                {
                    UserId = i.UserId,
                    FromDate = i.Range!.Value.From,
                    ToDate = i.Range.Value.To,
                    Reason = reason.Length > 100 ? reason[..100] : reason,
                    CreatedAt = now
                }).ToList();
            if (rows.Count == 0) return;
            _context.AttendanceRecalcRequests.AddRange(rows);
            await _context.SaveChangesAsync();
        }
    }
}
