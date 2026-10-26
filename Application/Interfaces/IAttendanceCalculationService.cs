using Application.Features.Attendance.Shifts;
using Domain.Enums;

namespace Application.Interfaces
{
    /// <summary>
    /// شخص يُراقب حضوره: فعّال وله رقم بصمة. داخل فترة عمله وحدها تُحسب أيامه، ومن لا وردية له لا يُحسب
    /// (قرار المستخدم 2026-10-26: «لا وردية له يعني لا داعي لمراقبة دوامه»).
    /// </summary>
    public sealed record MonitoredPerson(int UserId, string Name, UserKind Kind, string Number,
        int? AreaId, string? AreaName, int? BranchId, string? BranchName, int? DepartmentId, string? DepartmentName, int? OfficeId,
        DateTime? ActiveFrom, DateTime? ActiveTo)
    {
        public PersonUnits Units => new(UserId, AreaId, BranchId, DepartmentId, OfficeId);
        public bool ActiveOn(DateTime date) => (ActiveFrom == null || ActiveFrom <= date) && (ActiveTo == null || ActiveTo >= date);
    }

    /// <summary>الجدول الفعلي لشخص في يوم، مع ما يغلبه (إجازة، عطلة) وهل اليوم داخل فترة عمله</summary>
    public sealed record EffectiveDay(DateTime Date, DayPlan Plan, string? HolidayName, bool OnVacation, bool Active);

    public interface IAttendanceCalculationService
    {
        /// <summary>الأشخاص المراقَبون ضمن حدّ منطقة (للبحث عمّن لا وردية له)</summary>
        Task<List<MonitoredPerson>> MonitoredPeopleAsync(AreaLimit limit);

        /// <summary>صاحب بيانات حضور (ولو لم يكن فعّالاً) أو null</summary>
        Task<MonitoredPerson?> PersonAsync(int userId);

        /// <summary>الجدول الفعلي لكل يوم في الفترة لهؤلاء الأشخاص (من كانت له بيانات حضور ولو لم يكن فعّالاً)</summary>
        Task<Dictionary<int, List<EffectiveDay>>> EffectiveAsync(IReadOnlyCollection<int> userIds, DateTime from, DateTime to);

        /// <summary>
        /// يعيد حساب الأيام (userIds فارغ = كل المراقَبين) ويحفظ ما تغيّر فقط. الأيام غير المجدولة وخارج فترة العمل تُحذف نتائجها.
        /// يعيد عدد الأيام التي تغيّرت.
        /// </summary>
        Task<int> RecalculateAsync(IReadOnlyCollection<int>? userIds, DateTime from, DateTime to, CancellationToken ct = default);

        /// <summary>يحسب ما في طابور إعادة الحساب ثم يحذفه. يعيد عدد الطلبات المعالَجة</summary>
        Task<int> ProcessQueueAsync(CancellationToken ct = default);
    }
}
