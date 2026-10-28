using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>فلاتر قائمة أوامر الدراسة (كلها اختيارية، تُجمع بـ AND)</summary>
    public sealed record StudyFilter(int? ProjectId, int? BranchId, StudyStatus? Status, string? Search, bool OverdueOnly, int? AssigneeUserId, DateTime Today);

    /// <summary>مخرج مرشّح لتنبيه التأخر مع من يُنبَّه (المهندس المسؤول ومن أنشأ الأمر)</summary>
    public sealed record OverdueDeliverable(int DeliverableId, int StudyOrderId, string StudyTitle, string TypeName, int ProjectId, int BranchId,
        DateTime DueDate, int? AssigneeUserId, int CreatedByUserId);

    /// <summary>
    /// حفظ أوامر الدراسة ومخرجاتها وإرسالاتها وأنواع المخرجات. كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها نفسها (القاعدة 6)،
    /// وتغيّر الأمر بعد فتح المستخدم له يرفض الحفظ برسالة واضحة (rowVersion).
    /// </summary>
    public interface IStudyService
    {
        // ───────── أنواع المخرجات (إعدادات الفرع) ─────────
        Task<List<DeliverableType>> GetTypesAsync(int? branchId, bool includeInactive);
        Task<DeliverableType?> GetTypeAsync(int id);
        Task<DeliverableType> AddTypeAsync(DeliverableType type, AuditEntry audit);
        Task UpdateTypeAsync(DeliverableType type, AuditEntry audit);
        Task DeleteTypeAsync(DeliverableType type, AuditEntry audit);
        Task<bool> TypeNameExistsAsync(int branchId, string name, int? excludeId = null);
        Task<bool> TypeInUseAsync(int id);

        // ───────── الأوامر ─────────
        /// <summary>الأمر بمشروعه ومشاركيه ومخرجاته (بنوعها ومكتبها ومهندسها ووثيقتها وإصداراتها) وإرسالاته: ما تحتاجه القواعد في الذاكرة</summary>
        Task<StudyOrder?> GetByIdAsync(int id);

        /// <summary>أمر يحوي المخرج المطلوب (للعمليات على مخرج واحد)</summary>
        Task<StudyOrder?> GetByDeliverableAsync(int deliverableId);

        Task<StudyOrder?> GetBySubmissionAsync(int submissionId);

        Task<(List<StudyOrder> Items, int TotalCount)> SearchAsync(
            System.Linq.Expressions.Expression<Func<Project, bool>> visibleProjects, StudyFilter filter, int page, int pageSize);

        Task<StudyOrder> AddOrderAsync(StudyOrder order, AuditEntry audit);

        /// <summary>يحفظ تغييرات الأمر المحمَّل (حالة، ترتيب، عنوان…) وأثرها معاً. rowVersion = ما رآه المستخدم</summary>
        Task UpdateOrderAsync(StudyOrder order, byte[]? rowVersion, AuditEntry audit);

        // ───────── المخرجات ─────────
        Task AddDeliverableAsync(StudyOrder order, StudyDeliverable deliverable, AuditEntry audit);
        Task RemoveDeliverableAsync(StudyOrder order, StudyDeliverable deliverable, AuditEntry audit);

        /// <summary>يحفظ تغييراً على مخرج محمَّل (إسناد، رفع للمراجعة، إعادة…) وأثره معاً</summary>
        Task UpdateDeliverableAsync(StudyOrder order, StudyDeliverable deliverable, byte[]? rowVersion, AuditEntry audit);

        /// <summary>
        /// اعتماد المخرج: يحفظه، ويضع الإصدار المثبَّت «معتمداً» في إضبارة المشروع إن لم يكن، والأثر — في معاملة واحدة.
        /// </summary>
        Task ApproveDeliverableAsync(StudyOrder order, StudyDeliverable deliverable, int versionId, int reviewerId, byte[]? rowVersion, AuditEntry audit);

        // ───────── الإرسال إلى المركزية ─────────
        Task AddSubmissionAsync(StudyOrder order, StudySubmission submission, AuditEntry audit);
        Task DecideSubmissionAsync(StudyOrder order, StudySubmission submission, byte[]? rowVersion, AuditEntry audit);

        // ───────── التذكيرات ─────────
        /// <summary>مخرجات لم تُعتمد وتجاوزت موعدها في أوامر مفتوحة ولم يُنبَّه عن موعدها بعد</summary>
        Task<List<OverdueDeliverable>> GetOverdueCandidatesAsync(DateTime today, int take);
        Task MarkOverdueNotifiedAsync(int deliverableId, DateTime dueDate);

        // ───────── إعدادات المستند ─────────
        /// <summary>وثيقة من إضبارة المشروع بإصداراتها (لربط ملف المخرج أو وثيقة القرار)</summary>
        Task<ProjectDocument?> GetDocumentAsync(int documentId);
        Task<ProjectDocumentVersion?> GetVersionAsync(int versionId);
        /// <summary>الوثيقة مربوطة بمخرج في أي أمر دراسة</summary>
        Task<bool> DocumentInUseAsync(int documentId);
    }
}
