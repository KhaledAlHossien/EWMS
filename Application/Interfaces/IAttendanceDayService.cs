using Domain.Entities.Attendance;
using Domain.Enums;

namespace Application.Interfaces
{
    /// <summary>فلتر «الحضور اليومي». Status: present / late / early / absent / incomplete / excused / pending / vacation / holiday / rest</summary>
    public sealed record DailyFilter(DateTime Date, int? AreaId, int? BranchId, int? DepartmentId, string? Status, string? Query);

    /// <summary>نتيجة يوم مع صاحبها</summary>
    public sealed record DayRow(AttendanceDay Day, string Name, UserKind Kind, string Number, string? BranchName, string? DepartmentName, string? ShiftName);

    /// <summary>عدّادات اليوم ضمن الحد والفلاتر (عدا فلتر الحالة)</summary>
    public sealed record DailyCounts(int Total, int Present, int Late, int Early, int Absent, int Incomplete, int Excused, int Pending,
        int Vacation, int Holiday, int Rest);

    /// <summary>بصمة كما تظهر في كشف الشخص: خام (قد تكون متجاهلة) أو يدوية من تصحيح</summary>
    public sealed record SheetPunch(long? PunchId, long? CorrectionId, DateTime Time, string? DeviceName, int VerifyType, bool Manual, bool Ignored);

    public interface IAttendanceDayService
    {
        Task<(List<DayRow> Items, int Total, DailyCounts Counts)> DailyAsync(AttendanceScope scope, DailyFilter filter, int page, int pageSize);
        /// <summary>هل الشخص ضمن حد العرض (نفسه، قسمه، فرعه، منطقته، أو الكل)</summary>
        Task<bool> PersonInScopeAsync(AttendanceScope scope, int userId);
        /// <summary>نتائج أيام شخص في فترة مع تفصيلها</summary>
        Task<List<AttendanceDay>> DaysAsync(int userId, DateTime from, DateTime to);
        /// <summary>بصمات شخص في فترة (الخام، واليدوية من التصحيحات غير الملغاة، والمتجاهلة معلَّمة)</summary>
        Task<List<SheetPunch>> PunchesAsync(int userId, DateTime from, DateTime to);
        Task<Dictionary<int, string>> ShiftNamesAsync();
    }
}
