using Application.Features.Attendance.Shifts;
using Domain.Entities.Attendance;

namespace Attendance.Tests
{
    public class AttendanceCalculatorTests
    {
        private static readonly DateTime Day = new(2026, 10, 26);   // الاثنين
        private static readonly DateTime After = Day.AddDays(2);    // كل النوافذ أُغلقت

        /// <summary>08:00–15:00، الدخول 07:00–10:00، الخروج 13:00–18:00، سماح 10 دقائق</summary>
        private static TimetableSpec Morning(AttendanceLateCounting counting = AttendanceLateCounting.FromStart,
            bool requireIn = true, bool requireOut = true, int breakMinutes = 0) =>
            new(1, "صباحي", AttendanceTimetableKind.Fixed, 8 * 60, 15 * 60, 60, 120, 120, 180, 10, 10, counting,
                requireIn, requireOut, breakMinutes, 0);

        /// <summary>ليلي 22:00–06:00</summary>
        private static TimetableSpec Night() =>
            new(2, "ليلي", AttendanceTimetableKind.Fixed, 22 * 60, 6 * 60, 60, 120, 120, 120, 5, 5,
                AttendanceLateCounting.FromStart, true, true, 0, 0);

        /// <summary>مرن 7 ساعات بين 06:00 و20:00</summary>
        private static TimetableSpec Flexible() =>
            new(3, "مرن", AttendanceTimetableKind.Flexible, 6 * 60, 20 * 60, 0, 0, 0, 0, 0, 15,
                AttendanceLateCounting.FromStart, true, true, 0, 7 * 60);

        private static DateTime At(int hour, int minute = 0, int days = 0) => Day.AddDays(days).AddHours(hour).AddMinutes(minute);

        private static DayResult? Run(IReadOnlyList<TimetableSpec> timetables, IReadOnlyList<DateTime> punches, DateTime? now = null,
            bool vacation = false, bool holiday = false, bool wholeDayExcuse = false, IReadOnlyList<ExcusePeriod>? excuses = null,
            DayPlanKind kind = DayPlanKind.Work) =>
            AttendanceCalculator.Calculate(new DayInput(Day,
                new DayPlan(kind, 7, kind == DayPlanKind.Work ? timetables.Select(t => t.Id).ToList() : [], AttendanceScheduleTarget.Branch, false),
                timetables, vacation, holiday, wholeDayExcuse, excuses ?? [], punches, now ?? After));

        [Fact]
        public void On_time_is_present_without_late()
        {
            var r = Run([Morning()], [At(7, 55), At(15, 5)])!;
            Assert.Equal(AttendanceDayStatus.Present, r.Status);
            Assert.Equal(0, r.Late);
            Assert.Equal(0, r.Early);
            Assert.Equal(7 * 60, r.Work);
            Assert.Equal(At(7, 55), r.FirstIn);
            Assert.Equal(At(15, 5), r.LastOut);
        }

        [Fact]
        public void Late_within_grace_is_not_counted()
        {
            var r = Run([Morning()], [At(8, 10), At(15)])!;
            Assert.Equal(0, r.Late);
        }

        [Fact]
        public void Late_counts_from_start_by_default()
        {
            var r = Run([Morning()], [At(8, 15), At(15)])!;
            Assert.Equal(AttendanceDayStatus.Present, r.Status);
            Assert.Equal(15, r.Late);
        }

        [Fact]
        public void Late_after_grace_counts_only_the_excess()
        {
            var r = Run([Morning(AttendanceLateCounting.AfterGrace)], [At(8, 15), At(15)])!;
            Assert.Equal(5, r.Late);
        }

        [Fact]
        public void Early_leave_is_counted()
        {
            var r = Run([Morning()], [At(8), At(14, 30)])!;
            Assert.Equal(30, r.Early);
            Assert.Equal(6 * 60 + 30, r.Work);
        }

        [Fact]
        public void First_punch_in_window_is_check_in_and_last_is_check_out()
        {
            var r = Run([Morning()], [At(7, 50), At(7, 52), At(11), At(14, 58), At(15, 20)])!;
            Assert.Equal(At(7, 50), r.Parts[0].CheckIn);
            Assert.Equal(At(15, 20), r.Parts[0].CheckOut);
            Assert.Equal(5, r.PunchCount);
        }

        [Fact]
        public void Missing_check_out_is_incomplete_not_absent()
        {
            var r = Run([Morning()], [At(8)])!;
            Assert.Equal(AttendanceDayStatus.Incomplete, r.Status);
        }

        [Fact]
        public void Optional_check_out_counts_as_on_time()
        {
            var r = Run([Morning(requireOut: false)], [At(8)])!;
            Assert.Equal(AttendanceDayStatus.Present, r.Status);
            Assert.Equal(0, r.Early);
            Assert.Equal(7 * 60, r.Work);
        }

        [Fact]
        public void No_punch_in_any_window_is_absent()
        {
            var r = Run([Morning()], [At(12)])!;   // ظهراً: خارج النافذتين
            Assert.Equal(AttendanceDayStatus.Absent, r.Status);
        }

        [Fact]
        public void Open_windows_keep_the_day_pending()
        {
            var r = Run([Morning()], [At(8, 20)], now: At(11))!;
            Assert.Equal(AttendanceDayStatus.Pending, r.Status);
            Assert.Equal(20, r.Late);
            Assert.Equal(0, r.Early);
        }

        [Fact]
        public void Break_is_subtracted_from_work_and_required()
        {
            var r = Run([Morning(breakMinutes: 30)], [At(8), At(15)])!;
            Assert.Equal(6 * 60 + 30, r.Work);
            Assert.Equal(6 * 60 + 30, r.Required);
        }

        [Fact]
        public void Overnight_shift_belongs_to_the_check_in_date()
        {
            var r = Run([Night()], [At(22, 10), At(6, 2, days: 1)])!;
            Assert.Equal(AttendanceDayStatus.Present, r.Status);
            Assert.Equal(10, r.Late);
            Assert.Equal(At(6, 2, days: 1), r.LastOut);
            Assert.Equal(At(6, days: 1), r.Parts[0].PlannedEnd);
        }

        [Fact]
        public void Split_shift_sums_both_parts()
        {
            var first = new TimetableSpec(1, "صباحي", AttendanceTimetableKind.Fixed, 8 * 60, 12 * 60, 60, 60, 60, 60, 0, 0,
                AttendanceLateCounting.FromStart, true, true, 0, 0);
            var second = new TimetableSpec(2, "مسائي", AttendanceTimetableKind.Fixed, 16 * 60, 20 * 60, 60, 60, 60, 60, 0, 0,
                AttendanceLateCounting.FromStart, true, true, 0, 0);
            var r = Run([first, second], [At(8, 5), At(12), At(16), At(19, 50)])!;
            Assert.Equal(AttendanceDayStatus.Present, r.Status);
            Assert.Equal(2, r.Parts.Count);
            Assert.Equal(5, r.Late);
            Assert.Equal(10, r.Early);

            var half = Run([first, second], [At(8), At(12)])!;
            Assert.Equal(AttendanceDayStatus.Incomplete, half.Status);
        }

        [Fact]
        public void Flexible_counts_short_hours_after_grace()
        {
            var full = Run([Flexible()], [At(9), At(16, 10)])!;
            Assert.Equal(AttendanceDayStatus.Present, full.Status);
            Assert.Equal(0, full.Short);

            var shortDay = Run([Flexible()], [At(9), At(15)])!;
            Assert.Equal(60, shortDay.Short);
            Assert.Equal(0, shortDay.Late);

            var one = Run([Flexible()], [At(9)])!;
            Assert.Equal(AttendanceDayStatus.Incomplete, one.Status);
        }

        [Fact]
        public void Vacation_wins_over_holiday_and_work()
        {
            var r = Run([Morning()], [At(9), At(12)], vacation: true, holiday: true)!;
            Assert.Equal(AttendanceDayType.Vacation, r.Type);
            Assert.Equal(AttendanceDayStatus.Vacation, r.Status);
            Assert.Equal(180, r.Work);   // للعلم فقط
            Assert.Equal(0, r.Late);
        }

        [Fact]
        public void Holiday_and_rest_show_punches_without_absence()
        {
            Assert.Equal(AttendanceDayStatus.Holiday, Run([Morning()], [], holiday: true)!.Status);
            var rest = Run([], [At(9), At(11)], kind: DayPlanKind.Rest)!;
            Assert.Equal(AttendanceDayStatus.Rest, rest.Status);
            Assert.Equal(120, rest.Work);
        }

        [Fact]
        public void Unscheduled_day_has_no_result()
        {
            Assert.Null(Run([], [At(8), At(15)], kind: DayPlanKind.Unscheduled));
        }

        [Fact]
        public void Whole_day_excuse_is_excused()
        {
            var r = Run([Morning()], [], wholeDayExcuse: true)!;
            Assert.Equal(AttendanceDayStatus.Excused, r.Status);
        }

        [Fact]
        public void Morning_permission_moves_the_start()
        {
            // إذن 08:00–10:00: الدخول 10:05 ضمن السماح من بداية الدوام الجديدة
            var r = Run([Morning()], [At(10, 5), At(15)], excuses: [new ExcusePeriod(At(8), At(10))])!;
            Assert.Equal(0, r.Late);
            Assert.True(r.HasExcuse);
        }

        [Fact]
        public void Evening_permission_moves_the_end()
        {
            var r = Run([Morning()], [At(8), At(13, 30)], excuses: [new ExcusePeriod(At(13, 30), At(15))])!;
            Assert.Equal(0, r.Early);
            Assert.Equal(AttendanceDayStatus.Present, r.Status);
        }
    }

    public class AttendanceScheduleResolverTests
    {
        private static readonly DateTime Monday = new(2026, 10, 26);
        private static readonly PersonUnits Person = new(10, 1, 2, 3, 4);

        private static readonly Dictionary<int, ShiftSpec> Shifts = new()
        {
            // أسبوعية من السبت: الأحد–الخميس جدول 100
            [1] = new ShiftSpec(1, AttendanceCycleUnit.Week, 1, new DateTime(2026, 1, 3),
                new Dictionary<int, IReadOnlyList<int>> { [1] = [100], [2] = [100], [3] = [100], [4] = [100], [5] = [100] }),
            // كل يوم جدول 200
            [2] = new ShiftSpec(2, AttendanceCycleUnit.Day, 1, new DateTime(2026, 1, 1),
                new Dictionary<int, IReadOnlyList<int>> { [0] = [200] })
        };

        [Fact]
        public void Most_specific_schedule_wins()
        {
            var schedules = new List<ScheduleSpan>
            {
                new(AttendanceScheduleTarget.Branch, 2, 1, new DateTime(2026, 1, 1), null),
                new(AttendanceScheduleTarget.Person, 10, 2, new DateTime(2026, 10, 1), null)
            };
            var plan = AttendanceScheduleResolver.Resolve(Monday, Person, schedules, [], Shifts);
            Assert.Equal(AttendanceScheduleTarget.Person, plan.Source);
            Assert.Equal([200], plan.TimetableIds);

            var before = AttendanceScheduleResolver.Resolve(new DateTime(2026, 9, 28), Person, schedules, [], Shifts);
            Assert.Equal(AttendanceScheduleTarget.Branch, before.Source);
        }

        [Fact]
        public void Weekly_cycle_rest_day_is_rest()
        {
            var schedules = new List<ScheduleSpan> { new(AttendanceScheduleTarget.Area, 1, 1, new DateTime(2026, 1, 1), null) };
            var friday = AttendanceScheduleResolver.Resolve(new DateTime(2026, 10, 30), Person, schedules, [], Shifts);
            Assert.Equal(DayPlanKind.Rest, friday.Kind);
            Assert.Equal(DayPlanKind.Work, AttendanceScheduleResolver.Resolve(Monday, Person, schedules, [], Shifts).Kind);
        }

        [Fact]
        public void Temporary_beats_every_regular_schedule()
        {
            var schedules = new List<ScheduleSpan> { new(AttendanceScheduleTarget.Person, 10, 1, new DateTime(2026, 1, 1), null) };
            var temporaries = new List<TemporarySpan> { new(AttendanceScheduleTarget.Branch, 2, Monday, Monday, null) };
            var plan = AttendanceScheduleResolver.Resolve(Monday, Person, schedules, temporaries, Shifts);
            Assert.Equal(DayPlanKind.Rest, plan.Kind);
            Assert.True(plan.Temporary);
        }

        [Fact]
        public void No_schedule_is_unscheduled()
        {
            Assert.Equal(DayPlanKind.Unscheduled, AttendanceScheduleResolver.Resolve(Monday, Person, [], [], Shifts).Kind);
        }

        [Theory]
        [InlineData("2026-10-24", 0)]   // سبت
        [InlineData("2026-10-30", 6)]   // جمعة
        [InlineData("2026-10-31", 7)]   // سبت الأسبوع الثاني
        [InlineData("2026-10-17", 7)]   // قبل البداية: الدورة تتكرر للخلف
        public void Two_week_cycle_index(string date, int expected) =>
            Assert.Equal(expected, AttendanceShiftRules.DayIndex(AttendanceCycleUnit.Week, 2, new DateTime(2026, 10, 24), DateTime.Parse(date)));
    }
}
