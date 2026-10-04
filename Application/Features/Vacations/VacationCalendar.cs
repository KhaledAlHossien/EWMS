using Domain.Entities;

namespace Application.Features.Vacations
{
    /// <summary>
    /// حساب أيام الإجازة وتوزيع الدفع (قرار المستخدم 2026-10-04):
    /// - العطلة الأسبوعية الجمعة، والعطل الرسمية (PublicHoliday) لا تُحسب من مدة الإجازة.
    /// - الدفع يُحدَّد عند الاعتماد النهائي: حد شهري من الأيام المدفوعة (VacationRules.MaxPaidVacationDaysPerMonth)
    ///   مشترك بين كل الأنواع المدفوعة، ويُحسب من الإجازات المعتمدة فقط.
    /// </summary>
    public static class VacationCalendar
    {
        public const DayOfWeek WeeklyHoliday = DayOfWeek.Friday;

        public static bool IsWorkingDay(DateTime date, IReadOnlySet<DateTime> holidays) =>
            date.DayOfWeek != WeeklyHoliday && !holidays.Contains(date.Date);

        public static int CountWorkingDays(DateTime start, DateTime end, IReadOnlySet<DateTime> holidays)
        {
            var count = 0;
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (IsWorkingDay(d, holidays)) count++;
            return count;
        }

        /// <summary>
        /// يوزّع أيام العمل في المدة على أجزاء: جزء جديد عند تغيّر الشهر أو تغيّر حالة الدفع.
        /// usedPaid = الأيام المدفوعة المعتمدة مسبقاً لكل شهر (لا تشمل هذه الإجازة) — تُحدَّث أثناء التوزيع.
        /// </summary>
        public static List<VacationSegment> Allocate(
            DateTime start, DateTime end, bool typeIsPaid,
            IReadOnlySet<DateTime> holidays, Dictionary<(int Year, int Month), int> usedPaid)
        {
            var segments = new List<VacationSegment>();
            VacationSegment? current = null;

            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                if (!IsWorkingDay(d, holidays)) continue;

                var month = (d.Year, d.Month);
                var used = usedPaid.GetValueOrDefault(month);
                var paid = typeIsPaid && used < VacationRules.MaxPaidVacationDaysPerMonth;
                if (paid) usedPaid[month] = used + 1;

                if (current == null || current.IsPaid != paid
                    || current.StartDate.Year != d.Year || current.StartDate.Month != d.Month)
                {
                    current = new VacationSegment { StartDate = d, EndDate = d, IsPaid = paid, Days = 0 };
                    segments.Add(current);
                }

                current.EndDate = d;
                current.Days++;
            }

            return segments;
        }
    }
}
