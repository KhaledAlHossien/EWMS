using Application.Features.Vacations;
using Domain.Entities;
using Domain.Enums;

namespace Projects.Tests.Regression;

/// <summary>
/// حساب أيام الإجازة وتوزيع الدفع (قرار 2026-10-04): الجمعة والعطل الرسمية لا تُحسب، والحد المدفوع شهري مشترك بين الأنواع
/// ويُحسب من المعتمد فقط. خطأ هنا يغيّر أجر موظف، لذلك تُثبَّت الحالات الحدّية.
/// </summary>
public class VacationCalendarTests
{
    private static readonly IReadOnlySet<DateTime> NoHolidays = new HashSet<DateTime>();
    private static DateTime D(int month, int day) => new(2026, month, day);

    [Fact]
    public void The_calendar_assumptions_of_these_tests_hold()
    {
        // نوفمبر 2026: الأحد 1، الجمعة 6، الجمعة 13
        Assert.Equal(DayOfWeek.Sunday, D(11, 1).DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, D(11, 6).DayOfWeek);
    }

    [Fact]
    public void Friday_is_the_weekly_holiday_and_is_not_counted()
    {
        Assert.Equal(DayOfWeek.Friday, VacationCalendar.WeeklyHoliday);
        Assert.False(VacationCalendar.IsWorkingDay(D(11, 6), NoHolidays));
        Assert.True(VacationCalendar.IsWorkingDay(D(11, 5), NoHolidays));
        Assert.True(VacationCalendar.IsWorkingDay(D(11, 7), NoHolidays), "السبت يوم عمل");
    }

    [Fact]
    public void A_week_from_Sunday_to_Saturday_has_six_working_days()
    {
        Assert.Equal(6, VacationCalendar.CountWorkingDays(D(11, 1), D(11, 7), NoHolidays));
    }

    [Fact]
    public void Public_holidays_are_not_counted_and_the_time_of_day_is_ignored()
    {
        var holidays = new HashSet<DateTime> { D(11, 3) };
        Assert.Equal(5, VacationCalendar.CountWorkingDays(D(11, 1), D(11, 7), holidays));
        Assert.False(VacationCalendar.IsWorkingDay(D(11, 3).AddHours(15), holidays));
    }

    [Fact]
    public void A_holiday_on_a_friday_is_not_subtracted_twice()
    {
        var holidays = new HashSet<DateTime> { D(11, 6) };
        Assert.Equal(6, VacationCalendar.CountWorkingDays(D(11, 1), D(11, 7), holidays));
    }

    [Fact]
    public void A_range_made_only_of_holidays_has_no_working_days_and_no_segments()
    {
        Assert.Equal(0, VacationCalendar.CountWorkingDays(D(11, 6), D(11, 6), NoHolidays));
        Assert.Empty(VacationCalendar.Allocate(D(11, 6), D(11, 6), true, NoHolidays, []));
    }

    [Fact]
    public void A_single_day_range_counts_once()
    {
        Assert.Equal(1, VacationCalendar.CountWorkingDays(D(11, 2), D(11, 2), NoHolidays));
    }

    [Fact]
    public void The_monthly_paid_limit_is_two_days()
    {
        Assert.Equal(2, VacationRules.MaxPaidVacationDaysPerMonth);
    }

    [Fact]
    public void A_paid_type_gets_two_paid_days_then_unpaid_in_the_same_month()
    {
        // الأحد→الخميس = 5 أيام عمل: يومان مدفوعان وثلاثة غير مدفوعة
        var segments = VacationCalendar.Allocate(D(11, 1), D(11, 5), true, NoHolidays, []);

        Assert.Collection(segments,
            s => { Assert.True(s.IsPaid); Assert.Equal(2, s.Days); Assert.Equal(D(11, 1), s.StartDate); Assert.Equal(D(11, 2), s.EndDate); },
            s => { Assert.False(s.IsPaid); Assert.Equal(3, s.Days); Assert.Equal(D(11, 3), s.StartDate); Assert.Equal(D(11, 5), s.EndDate); });
    }

    [Fact]
    public void A_non_paid_type_is_entirely_unpaid_and_uses_no_quota()
    {
        var used = new Dictionary<(int, int), int>();
        var segments = VacationCalendar.Allocate(D(11, 1), D(11, 5), false, NoHolidays, used);

        var only = Assert.Single(segments);
        Assert.False(only.IsPaid);
        Assert.Equal(5, only.Days);
        Assert.Empty(used);
    }

    [Fact]
    public void Paid_days_already_approved_in_the_month_reduce_what_is_left()
    {
        var used = new Dictionary<(int, int), int> { [(2026, 11)] = 1 };
        var segments = VacationCalendar.Allocate(D(11, 1), D(11, 3), true, NoHolidays, used);

        Assert.Equal(1, segments.Where(s => s.IsPaid).Sum(s => s.Days));
        Assert.Equal(2, segments.Where(s => !s.IsPaid).Sum(s => s.Days));
        Assert.Equal(2, used[(2026, 11)]);
    }

    [Fact]
    public void A_month_whose_quota_is_spent_gives_only_unpaid_days()
    {
        var used = new Dictionary<(int, int), int> { [(2026, 11)] = 2 };
        var segments = VacationCalendar.Allocate(D(11, 1), D(11, 2), true, NoHolidays, used);

        Assert.All(segments, s => Assert.False(s.IsPaid));
        Assert.Equal(2, used[(2026, 11)]);
    }

    [Fact]
    public void A_range_crossing_a_month_gets_a_fresh_quota_for_the_new_month_and_splits_there()
    {
        // 29 و30 أكتوبر (خميس والجمعة)... نختار: الأحد 25 → الأحد 1 نوفمبر
        var segments = VacationCalendar.Allocate(D(10, 25), D(11, 1), true, NoHolidays, []);

        // أكتوبر: 25،26 مدفوعان؛ 27،28،29،31 (السبت) غير مدفوعة (30 جمعة)؛ نوفمبر: 1 مدفوع
        Assert.Equal(7, segments.Sum(s => s.Days));
        Assert.Equal(3, segments.Where(s => s.IsPaid).Sum(s => s.Days));
        Assert.All(segments, s => Assert.Equal(s.StartDate.Month, s.EndDate.Month));
        Assert.Equal(1, segments.Last().Days);
        Assert.True(segments.Last().IsPaid);
    }

    [Fact]
    public void Segment_days_always_add_up_to_the_working_days()
    {
        var holidays = new HashSet<DateTime> { D(11, 3), D(11, 9) };
        var segments = VacationCalendar.Allocate(D(10, 28), D(11, 20), true, holidays, []);

        Assert.Equal(VacationCalendar.CountWorkingDays(D(10, 28), D(11, 20), holidays), segments.Sum(s => s.Days));
    }

    [Fact]
    public void A_holiday_inside_a_paid_run_does_not_break_it_into_two_segments()
    {
        // السبت 7 ثم الأحد 8 (الجمعة 6 عطلة بينهما غير موجودة هنا)؛ نتحقق أن العطلة داخل المدفوع لا تفتح جزءاً جديداً
        var holidays = new HashSet<DateTime> { D(11, 2) };
        var segments = VacationCalendar.Allocate(D(11, 1), D(11, 3), true, holidays, []);

        var only = Assert.Single(segments);
        Assert.True(only.IsPaid);
        Assert.Equal(2, only.Days);
    }

    [Theory]
    [InlineData(VacationStatus.PendingManager, 0, 0, "يُحدَّد الدفع عند الاعتماد")]
    [InlineData(VacationStatus.PendingBranchManager, 0, 0, "يُحدَّد الدفع عند الاعتماد")]
    [InlineData(VacationStatus.Approved, 3, 0, "مدفوعة")]
    [InlineData(VacationStatus.Approved, 0, 3, "غير مدفوعة")]
    [InlineData(VacationStatus.Approved, 2, 3, "2 مدفوع و3 غير مدفوع")]
    [InlineData(VacationStatus.Approved, 0, 0, "لا أيام عمل")]
    [InlineData(VacationStatus.Rejected, 0, 0, "—")]
    [InlineData(VacationStatus.Cancelled, 0, 0, "—")]
    public void Payment_text_follows_the_status(VacationStatus status, int paid, int unpaid, string expected)
    {
        Assert.Equal(expected, VacationRules.PaymentAr(status, paid, unpaid));
    }

    [Fact]
    public void The_request_number_uses_the_creation_year_and_five_digits()
    {
        Assert.Equal("VAC-2026-00042", VacationRules.RequestNumber(42, new DateTime(2026, 3, 1)));
        Assert.Equal("VAC-2027-12345", VacationRules.RequestNumber(12345, new DateTime(2027, 1, 1)));
    }

    [Fact]
    public void Every_defined_status_has_a_known_arabic_name()
    {
        foreach (var status in Enum.GetValues<VacationStatus>().Where(s => s != VacationStatus.Unspecified))
            Assert.NotEqual("غير معروفة", VacationRules.StatusAr(status));
    }

    [Fact]
    public void The_reserved_status_value_two_is_never_reused()
    {
        // القيمة 2 كانت «الرئيس الإداري» المحذوفة: سجلات قديمة قد تحملها، فلا تُعاد
        Assert.DoesNotContain(2, Enum.GetValues<VacationStatus>().Select(s => (int)s));
    }
}
