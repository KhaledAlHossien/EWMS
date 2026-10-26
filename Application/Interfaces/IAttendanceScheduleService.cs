using Domain.Entities.Attendance;

namespace Application.Interfaces
{
    /// <summary>جهة إسناد: اسمها وما يميّزها ومنطقتها (لحدّ «منطقتي»)؛ Exists=false لجهة محذوفة</summary>
    public sealed record ScheduleTargetInfo(AttendanceScheduleTarget Type, int Id, string Name, string Detail, int? AreaId, bool Exists);

    public interface IAttendanceScheduleService
    {
        /// <summary>الإسنادات (مع الوردية ومن أجراها)، اختيارياً لجهة واحدة</summary>
        Task<List<AttendanceSchedule>> GetSchedulesAsync(AttendanceScheduleTarget? type, int? targetId);
        /// <summary>متتبَّع</summary>
        Task<AttendanceSchedule?> GetScheduleAsync(int id);
        /// <summary>إسناد آخر للجهة نفسها يتداخل مع الفترة (to فارغ = مفتوح)</summary>
        Task<AttendanceSchedule?> FindOverlapAsync(AttendanceScheduleTarget type, int targetId, DateTime from, DateTime? to, int? excludeId);
        Task SaveScheduleAsync(AttendanceSchedule schedule, byte[]? rowVersion);
        Task DeleteScheduleAsync(AttendanceSchedule schedule);

        /// <summary>الجدولات المؤقتة التي تنتهي في since أو بعده</summary>
        Task<List<AttendanceTemporarySchedule>> GetTemporariesAsync(AttendanceScheduleTarget? type, int? targetId, DateTime since);
        Task<AttendanceTemporarySchedule?> GetTemporaryAsync(int id);
        Task<AttendanceTemporarySchedule?> FindTemporaryOverlapAsync(AttendanceScheduleTarget type, int targetId, DateTime from, DateTime to);
        Task AddTemporaryAsync(AttendanceTemporarySchedule temporary);
        Task DeleteTemporaryAsync(AttendanceTemporarySchedule temporary);

        /// <summary>أسماء الجهات ومناطقها دفعة واحدة</summary>
        Task<Dictionary<(AttendanceScheduleTarget Type, int Id), ScheduleTargetInfo>> TargetsAsync(IEnumerable<(AttendanceScheduleTarget Type, int Id)> keys);
        /// <summary>جهات من نوع واحد ضمن حد المنطقة، بحث بالاسم (والرقم للأشخاص)</summary>
        Task<List<ScheduleTargetInfo>> TargetOptionsAsync(AttendanceScheduleTarget type, string? query, AreaLimit limit);
        /// <summary>الأشخاص (أصحاب أرقام البصمة) تحت جهة — لإعادة حسابهم</summary>
        Task<List<int>> UsersOfTargetAsync(AttendanceScheduleTarget type, int targetId);
    }
}
