using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>فلاتر قائمة طلبات التسعير (كلها اختيارية، تُجمع بـ AND)</summary>
    public sealed record PricingFilter(int? BranchId, int? ProjectId, PricingRequestStatus? Status, string? Search, bool OverdueOnly, int? MineUserId, DateTime Today);

    /// <summary>فلاتر قائمة محاضر الاتفاق (كلها اختيارية، تُجمع بـ AND)</summary>
    public sealed record AgreementFilter(int? BranchId, AgreementStatus? Status, int? SupplierId, string? Search);

    /// <summary>
    /// حفظ طلبات التسعير وعروضها وأسعار بنودها ومحاضر الاتفاق. كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها نفسها (القاعدة 6)،
    /// وتغيّر السجل بعد فتح المستخدم له يرفض الحفظ برسالة واضحة (rowVersion).
    /// </summary>
    public interface IPricingService
    {
        // ───────── الطلبات ─────────
        /// <summary>الطلب بمشروعه ومشاركيه وعقده وبنوده وعروضه (بموردها وأسطرها) ومستخدميه: ما تحتاجه القواعد في الذاكرة</summary>
        Task<PricingRequest?> GetByIdAsync(int id);
        Task<PricingRequest?> GetByOfferAsync(int offerId);
        Task<PricingRequest?> GetByLineAsync(int lineId);

        Task<(List<PricingRequest> Items, int TotalCount)> SearchAsync(
            System.Linq.Expressions.Expression<Func<PricingRequest, bool>> visible, PricingFilter filter, int page, int pageSize);

        Task<PricingRequest> AddAsync(PricingRequest request, AuditEntry audit);

        /// <summary>يحفظ تغييرات الطلب المحمَّل (حالة، تكليف، توصية، قرار، إرسال…) وأثرها معاً. rowVersion = ما رآه المستخدم</summary>
        Task UpdateAsync(PricingRequest request, byte[]? rowVersion, AuditEntry audit);

        /// <summary>يستبدل بنود طلب لا عروض عليه ببنود جديدة، مع بيانات الطلب، والأثر معاً</summary>
        Task ReplaceItemsAsync(PricingRequest request, IReadOnlyCollection<PricingRequestItem> removed, IReadOnlyCollection<PricingRequestItem> added, byte[]? rowVersion, AuditEntry audit);

        // ───────── العروض وأسعار البنود ─────────
        Task AddOfferAsync(PricingRequest request, PricingOffer offer, AuditEntry audit);

        /// <summary>حفظ عرض معدَّل: الأسطر المعدَّلة تُحفظ، و<paramref name="removed"/> تُحذف صراحةً، والمضافة (في <c>Lines</c> بلا معرّف) تُدرج</summary>
        Task SaveOfferAsync(PricingRequest request, PricingOffer offer, IReadOnlyCollection<PricingOfferLine> removed, byte[]? rowVersion, AuditEntry audit);

        /// <summary>
        /// اختيار سطر لبند: يُلغى المختار سابقاً لهذا البند أولاً (فهرس فريد: سطر مختار واحد) ثم يُختار الجديد، والأثر في المعاملة نفسها.
        /// </summary>
        Task SelectLineAsync(PricingRequest request, PricingOfferLine line, PricingOfferLine? previous, string reason, AuditEntry audit);
        Task UnselectLineAsync(PricingRequest request, PricingOfferLine line, AuditEntry audit);

        // ───────── محاضر الاتفاق ─────────
        Task<PricingAgreement?> GetAgreementAsync(int id);
        Task<(List<PricingAgreement> Items, int TotalCount)> SearchAgreementsAsync(AgreementFilter filter, int page, int pageSize);
        Task<bool> AgreementNumberExistsAsync(int branchId, string number, int? excludeId = null);
        Task<PricingAgreement> AddAgreementAsync(PricingAgreement agreement, AuditEntry audit);
        /// <summary>حفظ محضر محمَّل (بيانات، حالة…) مع حذف البنود المستبعدة صراحةً، والأثر معاً</summary>
        Task SaveAgreementAsync(PricingAgreement agreement, IReadOnlyCollection<PricingAgreementLine> removed, byte[]? rowVersion, AuditEntry audit);

        // ───────── الوثائق ─────────
        Task<ProjectDocumentVersion?> GetVersionAsync(int versionId);
    }
}
