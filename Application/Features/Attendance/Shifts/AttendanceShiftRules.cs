using Domain.Entities.Attendance;

namespace Application.Features.Attendance.Shifts
{
    /// <summary>
    /// قواعد الجداول الزمنية والورديات (مصدر واحد للتحقق والحساب): الحدود، رقم اليوم في الدورة، وأسماء العرض.
    /// </summary>
    public static class AttendanceShiftRules
    {
        public const int DayMinutes = 24 * 60;
        public const int MaxWindowMinutes = 12 * 60;
        public const int MaxGraceMinutes = 180;
        public const int MaxBreakMinutes = 8 * 60;
        public const int MaxCycleWeeks = 4;
        public const int MaxCycleDays = 60;
        public const int MaxTimetablesPerDay = 4;

        /// <summary>طول الدورة بالأيام</summary>
        public static int CycleDays(AttendanceCycleUnit unit, int length) =>
            unit == AttendanceCycleUnit.Week ? 7 * length : length;

        /// <summary>بداية الدورة: لدورة الأسابيع سبت أسبوع تاريخ البداية، وإلا تاريخ البداية نفسه</summary>
        public static DateTime CycleAnchor(AttendanceCycleUnit unit, DateTime startDate)
        {
            var date = startDate.Date;
            if (unit != AttendanceCycleUnit.Week) return date;
            var back = ((int)date.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
            return date.AddDays(-back);
        }

        /// <summary>رقم اليوم في الدورة (0 = أول يوم) — يصح للتواريخ قبل البداية أيضاً</summary>
        public static int DayIndex(AttendanceCycleUnit unit, int length, DateTime startDate, DateTime date)
        {
            var days = CycleDays(unit, length);
            var offset = (int)(date.Date - CycleAnchor(unit, startDate)).TotalDays;
            return ((offset % days) + days) % days;
        }

        /// <summary>مدة الجدول بالدقائق (الليلي يعبر منتصف الليل؛ البداية = النهاية = 24 ساعة)</summary>
        public static int SpanMinutes(int startMinute, int endMinute) =>
            endMinute > startMinute ? endMinute - startMinute : endMinute + DayMinutes - startMinute;

        public static bool IsOvernight(int startMinute, int endMinute) => endMinute <= startMinute;

        public static string Clock(int minute) => $"{minute / 60 % 24:00}:{minute % 60:00}";

        public static string KindName(AttendanceTimetableKind kind) => kind switch
        {
            AttendanceTimetableKind.Fixed => "ثابت",
            AttendanceTimetableKind.Flexible => "مرن",
            _ => ""
        };

        public static string TargetName(AttendanceScheduleTarget target) => target switch
        {
            AttendanceScheduleTarget.Area => "منطقة",
            AttendanceScheduleTarget.Branch => "فرع",
            AttendanceScheduleTarget.Department => "قسم",
            AttendanceScheduleTarget.Office => "مكتب",
            AttendanceScheduleTarget.Person => "شخص",
            _ => ""
        };

        public static string StatusName(AttendanceDayStatus status) => status switch
        {
            AttendanceDayStatus.Pending => "جارٍ",
            AttendanceDayStatus.Present => "حاضر",
            AttendanceDayStatus.Absent => "غائب",
            AttendanceDayStatus.Incomplete => "بصمة ناقصة",
            AttendanceDayStatus.Excused => "مبرَّر",
            AttendanceDayStatus.Rest => "راحة",
            AttendanceDayStatus.Holiday => "عطلة رسمية",
            AttendanceDayStatus.Vacation => "إجازة",
            _ => ""
        };

        public static string ExcuseName(AttendanceExcuseKind kind) => kind switch
        {
            AttendanceExcuseKind.Mission => "مهمة رسمية",
            AttendanceExcuseKind.Permission => "إذن",
            AttendanceExcuseKind.Training => "دورة تدريبية",
            AttendanceExcuseKind.Other => "أخرى",
            _ => ""
        };

        /// <summary>
        /// يتحقق من جدول زمني ويرمي رسالة عربية عند الخطأ (يُستدعى من الإنشاء والتعديل).
        /// </summary>
        public static void Validate(AttendanceTimetable t)
        {
            static void Check(bool ok, string message)
            {
                if (!ok) throw new InvalidOperationException(message);
            }

            Check(t.StartMinute is >= 0 and < DayMinutes && t.EndMinute is >= 0 and < DayMinutes, "الوقت غير صالح");
            Check(t.Kind is AttendanceTimetableKind.Fixed or AttendanceTimetableKind.Flexible, "نوع الجدول غير صالح");
            Check(t.LateCounting is AttendanceLateCounting.FromStart or AttendanceLateCounting.AfterGrace, "طريقة عدّ التأخير غير صالحة");
            Check(t.LateGraceMinutes is >= 0 and <= MaxGraceMinutes && t.EarlyLeaveGraceMinutes is >= 0 and <= MaxGraceMinutes,
                $"دقائق السماح بين 0 و{MaxGraceMinutes}");
            var span = SpanMinutes(t.StartMinute, t.EndMinute);
            Check(t.BreakMinutes >= 0 && t.BreakMinutes < span && t.BreakMinutes <= MaxBreakMinutes, "مدة الاستراحة أطول من الجدول");

            if (t.Kind == AttendanceTimetableKind.Fixed)
            {
                Check(new[] { t.CheckInBeforeMinutes, t.CheckInAfterMinutes, t.CheckOutBeforeMinutes, t.CheckOutAfterMinutes }
                    .All(m => m is >= 0 and <= MaxWindowMinutes), $"نوافذ البصمة بين 0 و{MaxWindowMinutes / 60} ساعة");
                Check(t.CheckInAfterMinutes >= t.LateGraceMinutes, "نافذة الدخول تنتهي قبل انتهاء سماح التأخير");
                Check(t.CheckOutBeforeMinutes >= t.EarlyLeaveGraceMinutes, "نافذة الخروج تبدأ بعد بداية سماح الخروج المبكر");
                // نافذة الدخول تنتهي قبل أن تبدأ نافذة الخروج، كي لا تُعدّ بصمة واحدة دخولاً وخروجاً
                Check(t.CheckInAfterMinutes + t.CheckOutBeforeMinutes < span, "نافذتا الدخول والخروج متداخلتان — قصّرهما");
                Check(t.CheckInBeforeMinutes + span + t.CheckOutAfterMinutes <= DayMinutes + MaxWindowMinutes, "النوافذ طويلة جداً");
                t.RequiredMinutes = 0;
            }
            else
            {
                Check(t.RequiredMinutes > 0 && t.RequiredMinutes <= span - t.BreakMinutes, "الساعات المطلوبة أطول من نافذة اليوم");
                t.CheckInBeforeMinutes = t.CheckInAfterMinutes = t.CheckOutBeforeMinutes = t.CheckOutAfterMinutes = 0;
                t.LateGraceMinutes = 0;
                t.RequireCheckIn = t.RequireCheckOut = true;
            }
        }
    }
}
