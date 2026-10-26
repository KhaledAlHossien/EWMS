using Domain.Entities.Attendance;

namespace Application.Interfaces
{
    /// <summary>مكتبة الجداول الزمنية والورديات (مشتركة للمؤسسة)</summary>
    public interface IAttendanceShiftService
    {
        Task<List<AttendanceTimetable>> GetTimetablesAsync();
        /// <summary>متتبَّع</summary>
        Task<AttendanceTimetable?> GetTimetableAsync(int id);
        Task<bool> TimetableNameExistsAsync(string name, int? excludeId = null);
        Task SaveTimetableAsync(AttendanceTimetable timetable, byte[]? rowVersion);
        /// <summary>عدد الورديات (والجدولات المؤقتة) التي تستعمل كل جدول</summary>
        Task<Dictionary<int, int>> TimetableUsageAsync();
        Task DeleteTimetableAsync(AttendanceTimetable timetable);

        /// <summary>كل الورديات مع أيامها وجداولها</summary>
        Task<List<AttendanceShift>> GetShiftsAsync();
        /// <summary>متتبَّعة مع أيامها</summary>
        Task<AttendanceShift?> GetShiftAsync(int id);
        Task<bool> ShiftNameExistsAsync(string name, int? excludeId = null);
        /// <summary>يحفظ الوردية ويضع أيامها بالضبط كما أُرسلت (في حفظ واحد)</summary>
        Task SaveShiftAsync(AttendanceShift shift, byte[]? rowVersion, IReadOnlyCollection<(int DayIndex, int TimetableId)> days);
        /// <summary>عدد الإسنادات التي تستعمل كل وردية</summary>
        Task<Dictionary<int, int>> ShiftUsageAsync();
        Task DeleteShiftAsync(AttendanceShift shift);
    }

    /// <summary>طابور إعادة حساب الحضور: يُحسب ما فيه في الخدمة الخلفية خلال دقيقة</summary>
    public interface IAttendanceRecalcQueue
    {
        /// <summary>
        /// يضع طلباً لشخص (أو للكل إن كان userId فارغاً) في فترة. ما بعد اليوم لا يُحسب (يُقصّ)،
        /// وما قبل أقصى مدة للخلف يُقصّ كذلك.
        /// </summary>
        Task EnqueueAsync(int? userId, DateTime from, DateTime to, string reason);
        Task EnqueueManyAsync(IEnumerable<(int UserId, DateTime From, DateTime To)> items, string reason);
    }
}
