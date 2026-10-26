namespace Application.Interfaces
{
    /// <summary>جهاز تغيّرت حالة اتصاله منذ الفحص السابق</summary>
    public sealed record DeviceStatusChange(int DeviceId, string Name, int? AreaId, bool Online);

    /// <summary>
    /// مزامنة الأجهزة: يحسب لكل جهاز من يجب أن يكون عليه (أشخاص منطقته الفعّالون ذوو رقم البصمة ضمن فترة عملهم، ومن أُضيفت
    /// له منطقة الجهاز إضافيةً) ويضع أوامر الإضافة والتحديث والحذف وإرسال القوالب. لا يحذف إلا من وضعه هو.
    /// </summary>
    public interface IAttendanceSyncService
    {
        /// <summary>all=false: الأجهزة المعلَّمة «تحتاج مزامنة» فقط؛ all=true: كل الأجهزة المقبولة (تسوية دورية). يعيد عدد الأوامر</summary>
        Task<int> SyncAsync(bool all, CancellationToken ct = default);

        /// <summary>
        /// تغيّر ما يخص هؤلاء الأشخاص (الاسم، الرقم، الفرع، التفعيل، القوالب…): تُعلَّم أجهزة مناطقهم الحالية، ومناطق الفروع
        /// والمناطق التي كانوا فيها (ليُزالوا عنها).
        /// </summary>
        Task MarkUsersAsync(IReadOnlyCollection<int> userIds, IReadOnlyCollection<int>? previousBranchIds = null,
            IReadOnlyCollection<int>? previousAreaIds = null, CancellationToken ct = default);

        /// <summary>
        /// الأجهزة المقبولة التي انقطعت (لا اتصال قبل offlineBeforeUtc) أو عادت بعد إشعار انقطاعها: يحدّث علامة الإشعار ويسجّل الحدث.
        /// </summary>
        Task<List<DeviceStatusChange>> DetectStatusChangesAsync(DateTime offlineBeforeUtc, CancellationToken ct = default);

        /// <summary>
        /// صيانة دورية: حذف صور لحظة البصم الأقدم من photosBefore (بتوقيت الأجهزة)، وإنهاء مهلة الأوامر المرسلة بلا رد،
        /// وحذف سجل الأوامر المنتهية الأقدم من 90 يوماً. يعيد عدد الصور المحذوفة.
        /// </summary>
        Task<int> CleanupAsync(DateTime photosBefore, CancellationToken ct = default);
    }
}
