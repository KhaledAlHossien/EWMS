using Domain.Entities.Attendance;

namespace Application.Interfaces
{
    public interface IAttendanceCorrectionService
    {
        /// <summary>تصحيحات شخص التي تمسّ الفترة (مع من أجراها ومن ألغاها)، الأحدث أولاً</summary>
        Task<List<AttendanceCorrection>> ListAsync(int userId, DateTime from, DateTime to);
        /// <summary>متتبَّع</summary>
        Task<AttendanceCorrection?> GetAsync(long id);
        /// <summary>صاحب البصمة الخام ووقتها</summary>
        Task<(int? UserId, DateTime Time)?> PunchAsync(long punchId);
        /// <summary>البصمة متجاهلة بتصحيح غير ملغى</summary>
        Task<bool> IgnoredAsync(long punchId);
        Task AddAsync(AttendanceCorrection correction);
        Task SaveAsync();
    }
}
