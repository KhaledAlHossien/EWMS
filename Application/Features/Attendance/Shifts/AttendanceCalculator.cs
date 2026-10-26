using Domain.Entities.Attendance;

namespace Application.Features.Attendance.Shifts
{
    /// <summary>جدول زمني كما يدخل الحساب</summary>
    public sealed record TimetableSpec(
        int Id, string Name, AttendanceTimetableKind Kind, int StartMinute, int EndMinute,
        int CheckInBefore, int CheckInAfter, int CheckOutBefore, int CheckOutAfter,
        int LateGrace, int EarlyGrace, AttendanceLateCounting Counting,
        bool RequireCheckIn, bool RequireCheckOut, int BreakMinutes, int RequiredMinutes)
    {
        public static TimetableSpec From(AttendanceTimetable t) => new(
            t.Id, t.Name, t.Kind, t.StartMinute, t.EndMinute,
            t.CheckInBeforeMinutes, t.CheckInAfterMinutes, t.CheckOutBeforeMinutes, t.CheckOutAfterMinutes,
            t.LateGraceMinutes, t.EarlyLeaveGraceMinutes, t.LateCounting,
            t.RequireCheckIn, t.RequireCheckOut, t.BreakMinutes, t.RequiredMinutes);
    }

    /// <summary>وردية كما تدخل الحساب: جداول كل يوم في الدورة</summary>
    public sealed record ShiftSpec(int Id, AttendanceCycleUnit Unit, int Length, DateTime StartDate,
        IReadOnlyDictionary<int, IReadOnlyList<int>> DayTimetables);

    /// <summary>وحدات الشخص التي تُبحث فيها الجدولة (المنطقة = منطقة العامل، وإلا منطقة فرعه)</summary>
    public sealed record PersonUnits(int UserId, int? AreaId, int? BranchId, int? DepartmentId, int? OfficeId);

    public sealed record ScheduleSpan(AttendanceScheduleTarget Target, int TargetId, int ShiftId, DateTime From, DateTime? To);

    public sealed record TemporarySpan(AttendanceScheduleTarget Target, int TargetId, DateTime From, DateTime To, int? TimetableId);

    public enum DayPlanKind { Unscheduled, Rest, Work }

    /// <summary>الجدول الفعلي لشخص في يوم، ومن أين جاء</summary>
    public sealed record DayPlan(DayPlanKind Kind, int? ShiftId, IReadOnlyList<int> TimetableIds,
        AttendanceScheduleTarget? Source, bool Temporary)
    {
        public static readonly DayPlan Unscheduled = new(DayPlanKind.Unscheduled, null, [], null, false);
    }

    /// <summary>
    /// حلّ الجدول الفعلي: المؤقتة أولاً ثم العادية، وفي كل منهما الأخص يغلب (شخص، مكتب، قسم، فرع، منطقة).
    /// </summary>
    public static class AttendanceScheduleResolver
    {
        private static IEnumerable<(AttendanceScheduleTarget Target, int Id)> Levels(PersonUnits p)
        {
            yield return (AttendanceScheduleTarget.Person, p.UserId);
            if (p.OfficeId is int office) yield return (AttendanceScheduleTarget.Office, office);
            if (p.DepartmentId is int department) yield return (AttendanceScheduleTarget.Department, department);
            if (p.BranchId is int branch) yield return (AttendanceScheduleTarget.Branch, branch);
            if (p.AreaId is int area) yield return (AttendanceScheduleTarget.Area, area);
        }

        public static DayPlan Resolve(DateTime date, PersonUnits person, IReadOnlyCollection<ScheduleSpan> schedules,
            IReadOnlyCollection<TemporarySpan> temporaries, IReadOnlyDictionary<int, ShiftSpec> shifts)
        {
            var day = date.Date;
            foreach (var (target, id) in Levels(person))
            {
                var temp = temporaries.FirstOrDefault(t => t.Target == target && t.TargetId == id && t.From <= day && t.To >= day);
                if (temp != null)
                    return temp.TimetableId is int tt
                        ? new DayPlan(DayPlanKind.Work, null, [tt], target, true)
                        : new DayPlan(DayPlanKind.Rest, null, [], target, true);
            }
            foreach (var (target, id) in Levels(person))
            {
                var schedule = schedules.FirstOrDefault(s => s.Target == target && s.TargetId == id && s.From <= day && (s.To == null || s.To >= day));
                if (schedule == null || !shifts.TryGetValue(schedule.ShiftId, out var shift)) continue;
                var index = AttendanceShiftRules.DayIndex(shift.Unit, shift.Length, shift.StartDate, day);
                return shift.DayTimetables.TryGetValue(index, out var list) && list.Count > 0
                    ? new DayPlan(DayPlanKind.Work, shift.Id, list, target, false)
                    : new DayPlan(DayPlanKind.Rest, shift.Id, [], target, false);
            }
            return DayPlan.Unscheduled;
        }
    }

    /// <summary>تبرير فترة (جزء من يوم) يدخل الحساب</summary>
    public sealed record ExcusePeriod(DateTime From, DateTime To);

    public sealed record DayInput(
        DateTime Date,
        DayPlan Plan,
        IReadOnlyList<TimetableSpec> Timetables,
        bool OnVacation,
        bool IsHoliday,
        bool WholeDayExcuse,
        IReadOnlyList<ExcusePeriod> Excuses,
        /// <summary>بصمات الشخص (بعد التصحيحات) من اليوم السابق إلى ما بعد التالي</summary>
        IReadOnlyList<DateTime> Punches,
        DateTime Now);

    public sealed record PartResult(int Order, TimetableSpec Timetable, DateTime PlannedStart, DateTime PlannedEnd,
        DateTime? CheckIn, DateTime? CheckOut, AttendanceDayStatus Status, int Late, int Early, int Short, int Work, int Required);

    public sealed record DayResult(AttendanceDayType Type, AttendanceDayStatus Status, int? ShiftId,
        DateTime? FirstIn, DateTime? LastOut, int Late, int Early, int Short, int Work, int Required,
        int PunchCount, bool HasExcuse, IReadOnlyList<PartResult> Parts);

    /// <summary>
    /// محرك الحساب: نتيجة يوم واحد لشخص من جدوله الفعلي وبصماته وتصحيحاته. دالة نقية (تُختبر بلا قاعدة بيانات).
    /// نوع اليوم بالأولوية: إجازة، عطلة رسمية، (غير مجدول = لا نتيجة)، راحة، يوم عمل.
    /// في يوم العمل: أول بصمة في نافذة الدخول = الدخول، وآخر بصمة في نافذة الخروج = الخروج
    /// (حالة «دخول/خروج» التي يرسلها الجهاز لا تُستعمل — قرار المستخدم 2026-10-26).
    /// </summary>
    public static class AttendanceCalculator
    {
        public static DayResult? Calculate(DayInput input)
        {
            var day = input.Date.Date;
            if (input.OnVacation) return Info(AttendanceDayType.Vacation, AttendanceDayStatus.Vacation, input);
            if (input.IsHoliday) return Info(AttendanceDayType.Holiday, AttendanceDayStatus.Holiday, input);
            if (input.Plan.Kind == DayPlanKind.Unscheduled) return null;
            if (input.Plan.Kind == DayPlanKind.Rest) return Info(AttendanceDayType.Rest, AttendanceDayStatus.Rest, input);

            var specs = input.Plan.TimetableIds
                .Select(id => input.Timetables.FirstOrDefault(t => t.Id == id))
                .Where(t => t != null).Select(t => t!)
                .OrderBy(t => t.StartMinute).ToList();
            if (specs.Count == 0) return Info(AttendanceDayType.Rest, AttendanceDayStatus.Rest, input);

            var parts = specs.Select((t, i) => t.Kind == AttendanceTimetableKind.Fixed
                ? FixedPart(i + 1, t, day, input)
                : FlexiblePart(i + 1, t, day, input)).ToList();

            AttendanceDayStatus status;
            if (input.WholeDayExcuse)
                status = AttendanceDayStatus.Excused;
            else if (parts.Any(p => p.Status == AttendanceDayStatus.Pending))
                status = AttendanceDayStatus.Pending;
            else
            {
                var attended = parts.Count(p => p.Status is AttendanceDayStatus.Present or AttendanceDayStatus.Incomplete);
                var absent = parts.Count(p => p.Status == AttendanceDayStatus.Absent);
                if (attended == 0) status = absent > 0 ? AttendanceDayStatus.Absent : AttendanceDayStatus.Excused;
                else if (absent > 0 || parts.Any(p => p.Status == AttendanceDayStatus.Incomplete)) status = AttendanceDayStatus.Incomplete;
                else status = AttendanceDayStatus.Present;
            }

            var from = parts.Min(p => WindowFrom(p.Timetable, p.PlannedStart));
            var to = parts.Max(p => WindowTo(p.Timetable, p.PlannedEnd));
            var excused = input.WholeDayExcuse;
            return new DayResult(AttendanceDayType.Work, status, input.Plan.ShiftId,
                parts.Where(p => p.CheckIn != null).Select(p => p.CheckIn).Min(),
                parts.Where(p => p.CheckOut != null).Select(p => p.CheckOut).Max(),
                excused ? 0 : parts.Sum(p => p.Late), excused ? 0 : parts.Sum(p => p.Early), excused ? 0 : parts.Sum(p => p.Short),
                parts.Sum(p => p.Work), parts.Sum(p => p.Required),
                input.Punches.Count(p => p >= from && p <= to),
                input.WholeDayExcuse || input.Excuses.Any(e => e.From < to && e.To > from),
                parts);
        }

        /// <summary>يوم بلا دوام (إجازة، عطلة، راحة): بصماته تُعرض للعلم، بلا غياب ولا إضافي</summary>
        private static DayResult Info(AttendanceDayType type, AttendanceDayStatus status, DayInput input)
        {
            var day = input.Date.Date;
            var punches = input.Punches.Where(p => p >= day && p < day.AddDays(1)).OrderBy(p => p).ToList();
            DateTime? first = punches.Count > 0 ? punches[0] : null;
            DateTime? last = punches.Count > 1 ? punches[^1] : null;
            var work = first != null && last != null ? (int)(last.Value - first.Value).TotalMinutes : 0;
            return new DayResult(type, status, input.Plan.ShiftId, first, last, 0, 0, 0, work, 0, punches.Count, false, []);
        }

        private static DateTime WindowFrom(TimetableSpec t, DateTime start) =>
            t.Kind == AttendanceTimetableKind.Fixed ? start.AddMinutes(-t.CheckInBefore) : start;

        private static DateTime WindowTo(TimetableSpec t, DateTime end) =>
            t.Kind == AttendanceTimetableKind.Fixed ? end.AddMinutes(t.CheckOutAfter) : end;

        private static int Minutes(TimeSpan span) => span <= TimeSpan.Zero ? 0 : (int)span.TotalMinutes;

        /// <summary>دقائق التأخير أو الخروج المبكر بعد السماح، بطريقة العدّ المختارة في الجدول</summary>
        private static int Counted(int minutes, int grace, AttendanceLateCounting counting) =>
            minutes <= grace ? 0 : counting == AttendanceLateCounting.FromStart ? minutes : minutes - grace;

        private static int Overlap(DateTime aFrom, DateTime aTo, DateTime bFrom, DateTime bTo)
        {
            var from = aFrom > bFrom ? aFrom : bFrom;
            var to = aTo < bTo ? aTo : bTo;
            return Minutes(to - from);
        }

        private static PartResult FixedPart(int order, TimetableSpec t, DateTime day, DayInput input)
        {
            var start = day.AddMinutes(t.StartMinute);
            var end = start.AddMinutes(AttendanceShiftRules.SpanMinutes(t.StartMinute, t.EndMinute));
            var required = Math.Max(0, Minutes(end - start) - t.BreakMinutes);

            // التبرير يُزيح بداية الدوام أو نهايته (إذن صباحي أو مسائي)، ويُسقط الجزء إن غطّاه كله
            var effStart = start;
            var effEnd = end;
            foreach (var e in input.Excuses.OrderBy(e => e.From))
                if (e.From <= effStart && e.To > effStart) effStart = e.To;
            foreach (var e in input.Excuses.OrderByDescending(e => e.To))
                if (e.To >= effEnd && e.From < effEnd) effEnd = e.From;
            if (effStart >= effEnd)
                return new PartResult(order, t, start, end, null, null, AttendanceDayStatus.Excused, 0, 0, 0, 0, required);

            var inFrom = start.AddMinutes(-t.CheckInBefore);
            var inTo = start.AddMinutes(t.CheckInAfter);
            var outFrom = end.AddMinutes(-t.CheckOutBefore);
            var outTo = end.AddMinutes(t.CheckOutAfter);

            DateTime? checkIn = input.Punches.Where(p => p >= inFrom && p <= inTo).OrderBy(p => p).Cast<DateTime?>().FirstOrDefault();
            DateTime? checkOut = input.Punches.Where(p => p >= outFrom && p <= outTo && (checkIn == null || p > checkIn))
                .OrderByDescending(p => p).Cast<DateTime?>().FirstOrDefault();
            var closed = input.Now >= outTo;

            if (checkIn == null && checkOut == null)
                return new PartResult(order, t, start, end, null, null,
                    closed ? AttendanceDayStatus.Absent : AttendanceDayStatus.Pending, 0, 0, 0, 0, required);

            // بصمة غير إلزامية غابت = في وقتها؛ والمبرَّرة كذلك
            var inMissing = checkIn == null && t.RequireCheckIn && effStart == start;
            var outMissing = checkOut == null && t.RequireCheckOut && effEnd == end;
            var inEff = checkIn ?? (inMissing ? (DateTime?)null : effStart);
            var outEff = checkOut ?? (outMissing ? (DateTime?)null : effEnd);

            var late = checkIn is DateTime ci ? Counted(Minutes(ci - effStart), t.LateGrace, t.Counting) : 0;
            var early = checkOut is DateTime co ? Counted(Minutes(effEnd - co), t.EarlyGrace, t.Counting) : 0;
            var work = inEff is DateTime wi && outEff is DateTime wo
                ? Math.Max(0, Overlap(wi, wo, start, end) - t.BreakMinutes) : 0;

            var status = !closed ? AttendanceDayStatus.Pending
                : inMissing || outMissing ? AttendanceDayStatus.Incomplete
                : AttendanceDayStatus.Present;
            return new PartResult(order, t, start, end, checkIn, checkOut, status, late, closed ? early : 0, 0, work, required);
        }

        private static PartResult FlexiblePart(int order, TimetableSpec t, DateTime day, DayInput input)
        {
            var from = day.AddMinutes(t.StartMinute);
            var to = from.AddMinutes(AttendanceShiftRules.SpanMinutes(t.StartMinute, t.EndMinute));
            // التبرير يُحتسب من الساعات المطلوبة
            var credit = input.Excuses.Sum(e => Overlap(e.From, e.To, from, to));
            var required = Math.Max(0, t.RequiredMinutes - credit);
            if (required == 0)
                return new PartResult(order, t, from, to, null, null, AttendanceDayStatus.Excused, 0, 0, 0, 0, t.RequiredMinutes);

            var punches = input.Punches.Where(p => p >= from && p <= to).OrderBy(p => p).ToList();
            var closed = input.Now >= to;
            if (punches.Count == 0)
                return new PartResult(order, t, from, to, null, null,
                    closed ? AttendanceDayStatus.Absent : AttendanceDayStatus.Pending, 0, 0, 0, 0, t.RequiredMinutes);
            if (punches.Count == 1)
                return new PartResult(order, t, from, to, punches[0], null,
                    closed ? AttendanceDayStatus.Incomplete : AttendanceDayStatus.Pending, 0, 0, 0, 0, t.RequiredMinutes);

            var work = Math.Max(0, Minutes(punches[^1] - punches[0]) - t.BreakMinutes);
            var shortage = closed ? Counted(Math.Max(0, required - work), t.EarlyGrace, t.Counting) : 0;
            return new PartResult(order, t, from, to, punches[0], punches[^1],
                closed ? AttendanceDayStatus.Present : AttendanceDayStatus.Pending, 0, 0, shortage, work, t.RequiredMinutes);
        }
    }
}
