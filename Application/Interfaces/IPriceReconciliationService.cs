using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>فلاتر قائمة المطابقات (كلها اختيارية، تُجمع بـ AND)</summary>
    public sealed record ReconciliationFilter(int? BranchId, ReconciliationStatus? Status, int? SupplierId, int? ProjectId, string? Search);

    /// <summary>
    /// حفظ مطابقات الفواتير والكشوف مع بنودها ومراجعها المجمَّدة. كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها نفسها،
    /// وتغيّر السجل بعد فتح المستخدم له يرفض الحفظ برسالة واضحة (rowVersion).
    /// </summary>
    public interface IPriceReconciliationService
    {
        /// <summary>المطابقة ببنودها ومورّدها ومشروعها وعقدها ومن دقّق ومن اعتمد ومن أحال</summary>
        Task<PriceReconciliation?> GetByIdAsync(int id);

        Task<(List<PriceReconciliation> Items, int TotalCount)> SearchAsync(ReconciliationFilter filter, int page, int pageSize);

        Task<bool> DocumentExistsAsync(int branchId, int supplierId, ReconciliationDocumentKind kind, string number, int? excludeId = null);

        Task<PriceReconciliation> AddAsync(PriceReconciliation reconciliation, AuditEntry audit);

        /// <summary>يحفظ تغييرات المطابقة المحمَّلة (بنود جديدة تُضاف إلى Lines، والمحذوفة في removed) وأثرها معاً</summary>
        Task SaveAsync(PriceReconciliation reconciliation, IReadOnlyCollection<PriceReconciliationLine> removed, byte[]? rowVersion, AuditEntry audit);

        /// <summary>سطر العرض المختار بعرضه وطلبه وبنده (للمرجع)</summary>
        Task<PricingOfferLine?> GetOfferLineAsync(int lineId);

        /// <summary>الأسطر المختارة من طلبات معتمدة (أو أُرسلت نتيجتها) لمورد في فرع، لاختيار مرجع</summary>
        Task<List<PricingOfferLine>> GetOfferLineCandidatesAsync(int branchId, int supplierId, string? search, int take);

        /// <summary>الأسعار المرجعية المعتمدة للاستخدام الحالي لمورد في فرع، لاختيار مرجع</summary>
        Task<List<ReferencePrice>> GetReferencePriceCandidatesAsync(int branchId, int supplierId, string? search, int take);

        Task<ReferencePrice?> GetReferencePriceAsync(int id);

        Task<ProjectDocumentVersion?> GetVersionAsync(int versionId);
    }
}
