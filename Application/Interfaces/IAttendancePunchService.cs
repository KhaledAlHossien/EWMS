using Domain.Entities.Attendance;
using Domain.Enums;

namespace Application.Interfaces
{
    public sealed record PunchFilter(DateTime From, DateTime To, int? AreaId, int? DeviceId, int? UserId, string? Query, bool UnknownOnly);

    /// <summary>بصمة مع ما يُعرض معها (الشخص وقسمه، الجهاز ومنطقته، وهل لها صورة)</summary>
    public sealed record PunchRow(AttendancePunch Punch, string? PersonName, UserKind? PersonKind, string? DepartmentName,
        string DeviceName, string? AreaName, bool HasPhoto);

    public sealed record UnknownPinRow(string Pin, int Punches, DateTime FirstAt, DateTime LastAt, List<string> Devices);

    public interface IAttendancePunchService
    {
        /// <summary>
        /// البصمات ضمن الحد (الشخص في قسمي/فرعي/منطقتي أو أنا، أو الجهاز في منطقتي) والفلاتر، الأحدث أولاً، صفحة واحدة.
        /// </summary>
        Task<(List<PunchRow> Items, int Total)> SearchAsync(AttendanceScope scope, PunchFilter filter, int page, int pageSize);
        /// <summary>أرقام تصل ولا صاحب لها (من أجهزة منطقتي أو كل الأجهزة)</summary>
        Task<List<UnknownPinRow>> UnknownPinsAsync(AreaLimit limit, int? areaId);
        /// <summary>صورة لحظة البصم للبصمة (بحد العرض نفسه) أو null</summary>
        Task<AttendancePunchPhoto?> PhotoForAsync(long punchId);
        Task<byte[]?> PhotoDataAsync(long photoId);
        /// <summary>هل البصمة ضمن حد العرض</summary>
        Task<bool> InScopeAsync(AttendanceScope scope, long punchId);
    }
}
