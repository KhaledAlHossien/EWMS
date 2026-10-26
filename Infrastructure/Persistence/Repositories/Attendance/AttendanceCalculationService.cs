using System.Security.Cryptography;
using System.Text;
using Application.Features.Attendance.Shifts;
using Application.Interfaces;
using Domain.Entities.Attendance;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// حساب الحضور على دفعات: يُحمِّل مدخلات مجموعة أشخاص (البصمات، التصحيحات، الإجازات، العطل، الجدولة) باستعلامات قليلة،
    /// ثم يمرّ كل يوم على AttendanceCalculator (الدالة النقية) ويحفظ ما تغيّر فقط (بصمة النتيجة Hash).
    /// </summary>
    public class AttendanceCalculationService : IAttendanceCalculationService
    {
        private const int ChunkSize = 200;
        private const int QueueBatch = 1000;

        private readonly DataContext _context;
        private readonly IAttendanceSettings _settings;

        public AttendanceCalculationService(DataContext context, IAttendanceSettings settings)
        {
            _context = context;
            _settings = settings;
        }

        private DateTime LocalNow => DateTime.UtcNow.AddHours(_settings.TimeZoneHours);

        /// <summary>من له رقم بصمة (activeOnly: والفعّال وحده)</summary>
        private IQueryable<MonitoredPerson> People(bool activeOnly)
        {
            var profiles = _context.AttendanceProfiles.AsNoTracking().Where(p => p.AttendanceNumber != null);
            if (activeOnly) profiles = profiles.Where(p => p.User.IsActive);
            return profiles.Select(p => new MonitoredPerson(
                p.UserId, p.User.FullName, p.User.Kind, p.AttendanceNumber!,
                p.AreaId ?? p.User.Branch!.AttendanceAreaId,
                p.AreaId != null ? p.Area!.Name : p.User.Branch!.AttendanceArea!.Name,
                p.User.BranchId, p.User.Branch!.Name,
                p.User.DepartmentId, p.User.Department!.Name,
                p.User.OfficeId,
                p.ActiveFrom, p.ActiveTo));
        }

        public async Task<List<MonitoredPerson>> MonitoredPeopleAsync(AreaLimit limit)
        {
            if (limit.IsNothing) return [];
            var query = People(true);
            if (!limit.All)
            {
                var area = limit.AreaId;
                query = query.Where(p => p.AreaId == area);
            }
            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public Task<MonitoredPerson?> PersonAsync(int userId) =>
            People(false).FirstOrDefaultAsync(p => p.UserId == userId);

        // ── المكتبة: الجدولة والورديات والجداول (جداول صغيرة تُقرأ كاملة مرة لكل حساب)

        private sealed record Library(
            List<ScheduleSpan> Schedules,
            List<TemporarySpan> Temporaries,
            Dictionary<int, ShiftSpec> Shifts,
            List<TimetableSpec> Timetables,
            Dictionary<DateTime, string> Holidays);

        private async Task<Library> LibraryAsync(DateTime from, DateTime to)
        {
            var schedules = await _context.AttendanceSchedules.AsNoTracking()
                .Select(s => new ScheduleSpan(s.TargetType, s.TargetId, s.ShiftId, s.FromDate, s.ToDate)).ToListAsync();
            var temporaries = await _context.AttendanceTemporarySchedules.AsNoTracking()
                .Where(t => t.FromDate <= to.AddDays(1) && t.ToDate >= from.AddDays(-1))
                .Select(t => new TemporarySpan(t.TargetType, t.TargetId, t.FromDate, t.ToDate, t.TimetableId)).ToListAsync();
            var shifts = (await _context.AttendanceShifts.AsNoTracking().Include(s => s.Days).ToListAsync())
                .ToDictionary(s => s.Id, s => new ShiftSpec(s.Id, s.CycleUnit, s.CycleLength, s.StartDate,
                    s.Days.GroupBy(d => d.DayIndex).ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(d => d.TimetableId).ToList())));
            var timetables = (await _context.AttendanceTimetables.AsNoTracking().ToListAsync()).Select(TimetableSpec.From).ToList();

            var holidays = new Dictionary<DateTime, string>();
            foreach (var h in await _context.PublicHolidays.AsNoTracking().Where(h => h.StartDate <= to && h.EndDate >= from).ToListAsync())
                for (var d = h.StartDate.Date; d <= h.EndDate.Date; d = d.AddDays(1))
                    holidays[d] = h.Name;
            return new Library(schedules, temporaries, shifts, timetables, holidays);
        }

        private async Task<Dictionary<int, List<(DateTime From, DateTime To)>>> VacationsAsync(List<int> userIds, DateTime from, DateTime to) =>
            (await _context.Vacation.AsNoTracking()
                .Where(v => userIds.Contains(v.UserId) && v.Status == VacationStatus.Approved && v.StartVac <= to && v.EndVac >= from)
                .Select(v => new { v.UserId, v.StartVac, v.EndVac }).ToListAsync())
            .GroupBy(v => v.UserId)
            .ToDictionary(g => g.Key, g => g.Select(v => (v.StartVac.Date, v.EndVac.Date)).ToList());

        public async Task<Dictionary<int, List<EffectiveDay>>> EffectiveAsync(IReadOnlyCollection<int> userIds, DateTime from, DateTime to)
        {
            var ids = userIds.Distinct().ToList();
            var people = await People(false).Where(p => ids.Contains(p.UserId)).ToListAsync();
            var library = await LibraryAsync(from, to);
            var vacations = await VacationsAsync(ids, from, to);

            var result = new Dictionary<int, List<EffectiveDay>>();
            foreach (var person in people)
            {
                var own = vacations.GetValueOrDefault(person.UserId) ?? [];
                var days = new List<EffectiveDay>();
                for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
                    days.Add(new EffectiveDay(d,
                        AttendanceScheduleResolver.Resolve(d, person.Units, library.Schedules, library.Temporaries, library.Shifts),
                        library.Holidays.GetValueOrDefault(d), own.Any(v => v.From <= d && v.To >= d), person.ActiveOn(d)));
                result[person.UserId] = days;
            }
            return result;
        }

        public async Task<int> RecalculateAsync(IReadOnlyCollection<int>? userIds, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var range = Clamp(from, to);
            if (range == null) return 0;
            var library = await LibraryAsync(range.Value.From, range.Value.To);
            return await RecalculateCoreAsync(userIds, range.Value.From, range.Value.To, library, ct);
        }

        /// <summary>ما بعد اليوم لا يُحسب</summary>
        private (DateTime From, DateTime To)? Clamp(DateTime from, DateTime to)
        {
            var today = LocalNow.Date;
            var end = to.Date > today ? today : to.Date;
            return from.Date > end ? null : (from.Date, end);
        }

        private async Task<int> RecalculateCoreAsync(IReadOnlyCollection<int>? userIds, DateTime from, DateTime to, Library library, CancellationToken ct)
        {
            var query = People(true);
            if (userIds != null)
            {
                var ids = userIds.Distinct().ToList();
                query = query.Where(p => ids.Contains(p.UserId));
            }
            var people = await query.OrderBy(p => p.UserId).ToListAsync(ct);
            var changed = 0;
            foreach (var chunk in people.Chunk(ChunkSize))
            {
                ct.ThrowIfCancellationRequested();
                changed += await RecalculateChunkAsync(chunk, from, to, library, ct);
                _context.ChangeTracker.Clear();
            }
            return changed;
        }

        private async Task<int> RecalculateChunkAsync(MonitoredPerson[] people, DateTime from, DateTime to, Library library, CancellationToken ct)
        {
            var ids = people.Select(p => p.UserId).ToList();
            var punchFrom = from.AddDays(-1);
            var punchTo = to.AddDays(3);
            var punches = await _context.AttendancePunches.AsNoTracking()
                .Where(p => p.UserId != null && ids.Contains(p.UserId.Value) && p.PunchTime >= punchFrom && p.PunchTime < punchTo)
                .Select(p => new { UserId = p.UserId!.Value, p.Id, p.PunchTime }).ToListAsync(ct);
            var corrections = await _context.AttendanceCorrections.AsNoTracking()
                .Where(c => ids.Contains(c.UserId) && c.CancelledAt == null && c.FromDate <= to.AddDays(1) && c.ToDate >= from.AddDays(-1))
                .ToListAsync(ct);
            var vacations = await VacationsAsync(ids, from, to);
            var existing = await _context.AttendanceDays.AsNoTracking()
                .Where(d => ids.Contains(d.UserId) && d.Date >= from && d.Date <= to)
                .Select(d => new { d.Id, d.UserId, d.Date, d.Hash }).ToListAsync(ct);

            var punchesByUser = punches.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.ToList());
            var correctionsByUser = corrections.GroupBy(c => c.UserId).ToDictionary(g => g.Key, g => g.ToList());
            var existingByKey = existing.ToDictionary(d => (d.UserId, d.Date));
            var now = LocalNow;
            var calculatedAt = DateTime.UtcNow;

            var removeIds = new List<long>();
            var add = new List<AttendanceDay>();
            foreach (var person in people)
            {
                var own = correctionsByUser.GetValueOrDefault(person.UserId) ?? [];
                var ignored = own.Where(c => c.Kind == AttendanceCorrectionKind.IgnorePunch && c.PunchId != null).Select(c => c.PunchId!.Value).ToHashSet();
                var times = (punchesByUser.GetValueOrDefault(person.UserId) ?? [])
                    .Where(p => !ignored.Contains(p.Id)).Select(p => p.PunchTime)
                    .Concat(own.Where(c => c.Kind == AttendanceCorrectionKind.AddPunch && c.PunchTime != null).Select(c => c.PunchTime!.Value))
                    .OrderBy(t => t).ToList();
                var excuses = own.Where(c => c.Kind == AttendanceCorrectionKind.Excuse).ToList();
                var partial = excuses.Where(e => !e.WholeDays && e.FromAt != null && e.ToAt != null)
                    .Select(e => new ExcusePeriod(e.FromAt!.Value, e.ToAt!.Value)).ToList();
                var ownVacations = vacations.GetValueOrDefault(person.UserId) ?? [];

                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    DayResult? result = null;
                    if (person.ActiveOn(d))
                    {
                        var plan = AttendanceScheduleResolver.Resolve(d, person.Units, library.Schedules, library.Temporaries, library.Shifts);
                        // التبرير الجزئي يخص يومه وما يمسّه من الليلي: يُمرّر ما يقع حول اليوم والحاسب يأخذ المتقاطع مع النوافذ
                        var dayExcuses = partial.Where(e => e.From < d.AddDays(2) && e.To > d.AddDays(-1)).ToList();
                        result = AttendanceCalculator.Calculate(new DayInput(d, plan, library.Timetables,
                            ownVacations.Any(v => v.From <= d && v.To >= d),
                            library.Holidays.ContainsKey(d),
                            excuses.Any(e => e.WholeDays && e.FromDate <= d && e.ToDate >= d),
                            dayExcuses, times, now));
                    }

                    existingByKey.TryGetValue((person.UserId, d), out var old);
                    if (result == null)
                    {
                        if (old != null) removeIds.Add(old.Id);
                        continue;
                    }
                    var hash = Hash(result);
                    if (old != null && old.Hash == hash) continue;
                    if (old != null) removeIds.Add(old.Id);
                    add.Add(ToEntity(person.UserId, d, result, hash, calculatedAt));
                }
            }

            if (removeIds.Count == 0 && add.Count == 0) return 0;
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            foreach (var part in removeIds.Chunk(1000))
                await _context.AttendanceDays.Where(d => part.Contains(d.Id)).ExecuteDeleteAsync(ct);
            _context.AttendanceDays.AddRange(add);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Math.Max(removeIds.Count, add.Count);
        }

        private static AttendanceDay ToEntity(int userId, DateTime date, DayResult r, string hash, DateTime calculatedAt) => new()
        {
            UserId = userId,
            Date = date,
            Type = r.Type,
            Status = r.Status,
            ShiftId = r.ShiftId,
            FirstIn = r.FirstIn,
            LastOut = r.LastOut,
            LateMinutes = r.Late,
            EarlyMinutes = r.Early,
            ShortMinutes = r.Short,
            WorkMinutes = r.Work,
            RequiredMinutes = r.Required,
            PunchCount = r.PunchCount,
            HasExcuse = r.HasExcuse,
            Hash = hash,
            CalculatedAt = calculatedAt,
            Parts = r.Parts.Select(p => new AttendanceDayPart
            {
                Order = p.Order,
                TimetableId = p.Timetable.Id,
                TimetableName = p.Timetable.Name,
                Kind = p.Timetable.Kind,
                PlannedStart = p.PlannedStart,
                PlannedEnd = p.PlannedEnd,
                CheckIn = p.CheckIn,
                CheckOut = p.CheckOut,
                Status = p.Status,
                LateMinutes = p.Late,
                EarlyMinutes = p.Early,
                ShortMinutes = p.Short,
                WorkMinutes = p.Work,
                RequiredMinutes = p.Required
            }).ToList()
        };

        /// <summary>بصمة النتيجة: يوم لم يتغير لا يُعاد حفظه</summary>
        private static string Hash(DayResult r)
        {
            var text = new StringBuilder()
                .Append((int)r.Type).Append('|').Append((int)r.Status).Append('|').Append(r.ShiftId).Append('|')
                .Append(r.FirstIn?.Ticks).Append('|').Append(r.LastOut?.Ticks).Append('|')
                .Append(r.Late).Append('|').Append(r.Early).Append('|').Append(r.Short).Append('|')
                .Append(r.Work).Append('|').Append(r.Required).Append('|').Append(r.PunchCount).Append('|').Append(r.HasExcuse);
            foreach (var p in r.Parts)
                text.Append('#').Append(p.Order).Append('|').Append(p.Timetable.Id).Append('|').Append(p.Timetable.Name).Append('|')
                    .Append(p.PlannedStart.Ticks).Append('|').Append(p.PlannedEnd.Ticks).Append('|')
                    .Append(p.CheckIn?.Ticks).Append('|').Append(p.CheckOut?.Ticks).Append('|').Append((int)p.Status).Append('|')
                    .Append(p.Late).Append('|').Append(p.Early).Append('|').Append(p.Short).Append('|').Append(p.Work).Append('|').Append(p.Required);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }

        public async Task<int> ProcessQueueAsync(CancellationToken ct = default)
        {
            var batch = await _context.AttendanceRecalcRequests.AsNoTracking().OrderBy(r => r.Id).Take(QueueBatch).ToListAsync(ct);
            if (batch.Count == 0) return 0;

            var library = await LibraryAsync(batch.Min(r => r.FromDate), batch.Max(r => r.ToDate));
            var everyone = batch.Where(r => r.UserId == null).ToList();
            (DateTime From, DateTime To)? all = everyone.Count > 0 ? (everyone.Min(r => r.FromDate), everyone.Max(r => r.ToDate)) : null;
            if (all is (DateTime allFrom, DateTime allTo) && Clamp(allFrom, allTo) is (DateTime f, DateTime t))
                await RecalculateCoreAsync(null, f, t, library, ct);

            // طلبات الأشخاص: فترة واحدة لكل شخص، ويُجمع من تتطابق فترتهم في حساب واحد
            var perUser = batch.Where(r => r.UserId != null)
                .GroupBy(r => r.UserId!.Value)
                .Select(g => (UserId: g.Key, From: g.Min(r => r.FromDate), To: g.Max(r => r.ToDate)))
                .Where(u => all == null || u.From < all.Value.From || u.To > all.Value.To);
            foreach (var group in perUser.GroupBy(u => (u.From, u.To)))
                if (Clamp(group.Key.From, group.Key.To) is (DateTime gf, DateTime gt))
                    await RecalculateCoreAsync(group.Select(u => u.UserId).ToList(), gf, gt, library, ct);

            var done = batch.Select(r => r.Id).ToList();
            await _context.AttendanceRecalcRequests.Where(r => done.Contains(r.Id)).ExecuteDeleteAsync(ct);
            return batch.Count;
        }
    }
}
