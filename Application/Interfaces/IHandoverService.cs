using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>فلاتر قائمة محاضر الاستلام (كلها اختيارية، تُجمع بـ AND)</summary>
    public sealed record HandoverFilter(int? ProjectId, int? BranchId, HandoverKind? Kind, HandoverStatus? Status, string? Search, bool OpenNotesOnly, DateTime Today);

    /// <summary>ما يلزم لتنبيه إغلاق المشروع: سجلات الاستلام بملاحظاتها، والعقود بمحتجزاتها وقراراتها</summary>
    public sealed record ProjectClosureData(List<HandoverRecord> Records, List<Contract> Contracts);

    /// <summary>
    /// حفظ محاضر الاستلام ولجانها وملاحظاتها. كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها نفسها (القاعدة 6)،
    /// وتغيّر السجل بعد فتح المستخدم له يرفض الحفظ برسالة واضحة (rowVersion).
    /// </summary>
    public interface IHandoverService
    {
        /// <summary>المحضر بمشروعه ومشاركيه وعقده ولجنته وأعضائها وملاحظاته: ما تحتاجه القواعد في الذاكرة</summary>
        Task<HandoverRecord?> GetByIdAsync(int id);
        Task<HandoverRecord?> GetByNoteAsync(int noteId);

        /// <summary>كل محاضر المشروع بملاحظاتها (لقاعدة طلب واحد غير منتهٍ لكل نوع)</summary>
        Task<List<HandoverRecord>> GetByProjectAsync(int projectId);

        Task<(List<HandoverRecord> Items, int TotalCount)> SearchAsync(
            System.Linq.Expressions.Expression<Func<Project, bool>> visibleProjects, HandoverFilter filter, int page, int pageSize);

        /// <summary>ملاحظات لم تُغلق في محاضر المشروع الأخرى (غير <paramref name="excludeRecordId"/>)</summary>
        Task<int> CountOpenNotesElsewhereAsync(int projectId, int excludeRecordId);

        Task<ProjectClosureData> GetClosureDataAsync(int projectId);

        Task<HandoverRecord> AddAsync(HandoverRecord record, AuditEntry audit);

        /// <summary>يحفظ تغييرات المحضر المحمَّل (حالة، نتيجة، إلغاء…) وأثرها معاً. rowVersion = ما رآه المستخدم</summary>
        Task UpdateAsync(HandoverRecord record, byte[]? rowVersion, AuditEntry audit);

        /// <summary>
        /// تشكيل لجنة جديدة (يتحوّل المحضر إلى «لجنة مشكَّلة» في المعاملة نفسها، وrowVersion للمحضر) أو تعديلها (يُحذف القدامى ثم يُضاف
        /// <paramref name="added"/>، وrowVersion للجنة).
        /// </summary>
        Task SaveCommitteeAsync(HandoverRecord record, Committee committee, bool isNew, IReadOnlyCollection<CommitteeMember> added,
            IReadOnlyCollection<CommitteeMember> removed, byte[]? rowVersion, AuditEntry audit);

        Task AddNoteAsync(HandoverRecord record, HandoverNote note, AuditEntry audit);
        Task SaveNoteAsync(HandoverRecord record, HandoverNote note, byte[]? rowVersion, AuditEntry audit);
        Task RemoveNoteAsync(HandoverRecord record, HandoverNote note, AuditEntry audit);

        // ───────── التذكيرات ─────────
        /// <summary>ملاحظات لم تُغلق وتجاوزت موعد معالجتها ولم يُنبَّه عن موعدها بعد (بمشروعها ومشاركيه لتحديد المخوَّلين)</summary>
        Task<List<HandoverNote>> GetOverdueNotesAsync(DateTime today, int take);
        Task MarkNoteOverdueNotifiedAsync(int noteId, DateTime dueDate);

        // ───────── الوثائق ─────────
        Task<ProjectDocumentVersion?> GetVersionAsync(int versionId);
    }
}
