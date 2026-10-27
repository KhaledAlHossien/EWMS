using Application.Features.Schedules;
using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// حفظ أمر المباشرة والجدول الزمني. كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها نفسها (كالمشاريع).
    /// الكيانات المقروءة هنا متتبَّعة: يعدّلها المعالج ثم يستدعي دالة الحفظ المناسبة.
    /// </summary>
    public interface IScheduleService
    {
        // ───────── أمر المباشرة ─────────
        Task<ProjectStartOrder?> GetStartOrderAsync(int projectId);
        /// <summary>يضيف الأمر أو يحفظ تعديله (rowVersion = ما رآه المستخدم)</summary>
        Task SaveStartOrderAsync(ProjectStartOrder order, bool isNew, byte[]? rowVersion, AuditEntry audit, int branchId);

        // ───────── الإصدارات ─────────
        /// <summary>كل إصدارات الجدول مع عدد بنودها ونسبتها الفعلية (بلا تحميل البنود)</summary>
        Task<List<ScheduleVersionRow>> GetVersionsAsync(int projectId);
        /// <summary>الإصدار مع مشروعه ومشاركيه (للحدّ) وبنوده ومسؤوليها ومنشئه ومعتمده</summary>
        Task<ProjectSchedule?> GetByIdAsync(int id);
        Task<ProjectSchedule?> GetApprovedAsync(int projectId);
        Task<List<ScheduleDependency>> GetDependenciesAsync(int scheduleId);
        /// <summary>طلب تمديد مسودة أو مرفوع بانتظار القرار: لا يُنشأ إصدار جديد من الجدول قبل البتّ فيه</summary>
        Task<bool> HasPendingExtensionAsync(int projectId);

        /// <summary>
        /// ينشئ المسودة (برقم الإصدار التالي). إن كانت <paramref name="copies"/> غير فارغة فهي نسخ بنود المعتمد
        /// وتُعاد تبعياتها بمعرّفاتها الجديدة في المعاملة نفسها.
        /// </summary>
        Task<ProjectSchedule> CreateDraftAsync(ProjectSchedule draft, IReadOnlyList<(int OldId, ScheduleTask Copy)> copies,
            IReadOnlyList<ScheduleRules.Edge> edges, AuditEntry audit, int branchId);
        /// <summary>يحذف المسودة وتبعياتها وبنودها</summary>
        Task DeleteDraftAsync(ProjectSchedule draft, AuditEntry audit, int branchId);
        /// <summary>
        /// يعتمد المسودة: السابق المعتمد (إن وُجد) يصير «سابقاً» ثم تُثبَّت التواريخ الأصلية وتُعتمد المسودة، كله في معاملة واحدة.
        /// </summary>
        Task ApproveAsync(ProjectSchedule draft, ProjectSchedule? previousApproved, int approverId, AuditEntry audit, int branchId);

        // ───────── البنود والتبعيات (المسودة) ─────────
        Task<ScheduleTask?> GetTaskAsync(int taskId);
        Task AddTaskAsync(ScheduleTask task, AuditEntry audit, int branchId);
        /// <summary>يحفظ تعديل البند (rowVersion = ما رآه المستخدم)</summary>
        Task UpdateTaskAsync(ScheduleTask task, byte[]? rowVersion, AuditEntry audit, int branchId);
        /// <summary>يحذف البند وتبعياته</summary>
        Task DeleteTaskAsync(ScheduleTask task, AuditEntry audit, int branchId);
        Task ReorderAsync(int scheduleId, IReadOnlyList<int> orderedTaskIds, AuditEntry audit, int branchId);

        Task<ScheduleDependency?> GetDependencyAsync(int id);
        Task AddDependencyAsync(ScheduleDependency dependency, AuditEntry audit, int branchId);
        Task RemoveDependencyAsync(ScheduleDependency dependency, AuditEntry audit, int branchId);

        // ───────── الإنجاز (الجدول المعتمد) ─────────
        /// <summary>يحفظ تحديث الإنجاز وسطره في السجل معاً</summary>
        Task SaveProgressAsync(ScheduleTask task, ScheduleProgressEntry entry, byte[]? rowVersion, AuditEntry audit, int branchId);
        Task<List<ScheduleProgressEntry>> GetProgressHistoryAsync(int taskId, int take);
    }

    /// <summary>إصدار من الجدول مع عدد بنوده ونسبته الفعلية المرجَّحة</summary>
    public sealed record ScheduleVersionRow(ProjectSchedule Schedule, int TaskCount, decimal ActualProgress);
}
