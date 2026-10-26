using Domain.Entities.Attendance;

namespace Application.Interfaces
{
    /// <summary>جهاز مع عدد أوامره المنتظرة ومن وضعناهم عليه</summary>
    public sealed record DeviceRow(AttendanceDevice Device, string? AreaName, string? ApprovedByName, int PendingCommands, int ManagedUsers);

    public interface IAttendanceDeviceService
    {
        /// <summary>الأجهزة ضمن الحد (المنتظرة والمرفوضة والمُزالة بلا منطقة: لحدّ «الكل» فقط)، الأحدث اتصالاً أولاً</summary>
        Task<List<DeviceRow>> GetAllAsync(AreaLimit limit, AttendanceDeviceState? state, int? areaId);
        Task<DeviceRow?> GetRowAsync(int id);
        /// <summary>متتبَّع مع منطقته</summary>
        Task<AttendanceDevice?> GetByIdAsync(int id);
        /// <summary>يحفظ تعديل الجهاز وسطور سجله، ويُسقط لقطته من ذاكرة البروتوكول</summary>
        Task SaveAsync(AttendanceDevice device, params AttendanceDeviceLog[] logs);
        Task EnqueueAsync(IReadOnlyCollection<AttendanceDeviceCommand> commands);
        /// <summary>الأوامر التي لم تُرسل بعد تُلغى (إزالة الجهاز)</summary>
        Task CancelQueuedAsync(int deviceId);
        /// <summary>إعادة مزامنة كاملة: يُنسى ما أُرسل فيُعاد إرسال كل الأشخاص وقوالبهم</summary>
        Task RequestFullResyncAsync(int deviceId);
        Task MarkNeedsSyncAsync(int deviceId);
        Task<(List<AttendanceDeviceCommand> Items, int Total)> CommandsAsync(int deviceId, int page, int pageSize);
        Task<List<AttendanceDeviceLog>> LogAsync(int deviceId, int take);
        Task<List<AttendanceDeviceUser>> DeviceUsersAsync(int deviceId);
    }
}
