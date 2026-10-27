using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// حفظ التأخيرات وطلبات التمديد وقراءة البنود المتأخرة للتنبيه. كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها.
    /// الكيانات المقروءة متتبَّعة: يعدّلها المعالج (بما فيها بنود الجدول عند تطبيق التمديد) ثم يُستدعى الحفظ مرة واحدة فتُحفظ كلها معاً.
    /// </summary>
    public interface IScheduleChangeService
    {
        // ───────── التأخير ─────────
        Task<List<ScheduleDelay>> GetDelaysAsync(int projectId, int take);
        /// <summary>التأخير مع بنده وإصداره ومشروعه ومشاركيه (للحدّ) ومسجِّله وقراره ومستند موافقته وبنوده المتأثرة</summary>
        Task<ScheduleDelay?> GetDelayAsync(int id);
        /// <summary>تأخير على البند لم ينتهِ أثره (أي حالة غير «انتهى أثره»): لا يُسجَّل غيره حتى يُعالج</summary>
        Task<bool> HasLiveDelayAsync(int taskId);
        Task AddDelayAsync(ScheduleDelay delay, AuditEntry audit, int branchId);
        Task SaveDelayAsync(ScheduleDelay delay, byte[]? rowVersion, AuditEntry audit, int branchId);
        /// <summary>تأخيرات بنود ممدودة يُنهى أثرها بالتمديد (مسجَّلة أو معتمدة التبرير)</summary>
        Task<List<ScheduleDelay>> GetResolvableDelaysAsync(IReadOnlyCollection<int> taskIds);

        // ───────── التمديد ─────────
        Task<List<ScheduleExtension>> GetExtensionsAsync(int projectId, int take);
        /// <summary>الطلب مع بنوده ومستنداته وإصداره ومشروعه ومشاركيه وصاحبه وقراره</summary>
        Task<ScheduleExtension?> GetExtensionAsync(int id);
        Task AddExtensionAsync(ScheduleExtension extension, AuditEntry audit, int branchId);
        /// <summary>يحفظ كل ما تغيّر (الطلب وبنوده وبنود الجدول والتأخيرات) مع الأثر في معاملة واحدة</summary>
        Task SaveExtensionAsync(ScheduleExtension extension, byte[]? rowVersion, AuditEntry audit, int branchId);
        Task DeleteExtensionAsync(ScheduleExtension extension, AuditEntry audit, int branchId);

        // ───────── تنبيه مرشَّحي التأخر ─────────
        /// <summary>
        /// بنود جداول معتمدة لمشاريع مفتوحة تجاوزت نهايتها ولم تكتمل ولم يُنبَّه عن نهايتها الحالية بعد (مع مشروعها ومشاركيه)
        /// </summary>
        Task<List<ScheduleTask>> GetOverdueToNotifyAsync(DateTime today, int take);
        Task SaveChangesAsync();
    }
}
