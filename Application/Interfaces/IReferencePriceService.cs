using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>فلاتر قائمة الأسعار المرجعية (كلها اختيارية، تُجمع بـ AND). المُستبدَل مخفي ما لم يُطلب</summary>
    public sealed record ReferencePriceFilter(int? BranchId, string? Search, int? SupplierId, int? ProjectId, ReferencePriceStatus? Status, bool IncludeSuperseded, DateTime? From, DateTime? To);

    /// <summary>
    /// حفظ الأسعار المرجعية. سجل تاريخي: لا تعديل لبيانات السعر ولا حذف؛ التصحيح والاعتماد يغيّران الحالة وحدها.
    /// كل كتابة تأخذ <see cref="AuditEntry"/> وتحفظه في معاملتها نفسها، وتغيّر السجل بعد فتح المستخدم له يرفض الحفظ برسالة واضحة (rowVersion).
    /// </summary>
    public interface IReferencePriceService
    {
        /// <summary>السعر بمورده ومشروعه ومن أضافه ومن اعتمده والسجل الذي يصحّحه</summary>
        Task<ReferencePrice?> GetByIdAsync(int id);

        Task<(List<ReferencePrice> Items, int TotalCount)> SearchAsync(ReferencePriceFilter filter, int page, int pageSize);

        /// <summary>
        /// كل أسعار مجموعات المواد التي تخص السجلات المعطاة (بالمفتاح: رمز أو اسم، ثم الوحدة والعملة) — لحساب «آخر سعر» والتاريخ.
        /// includeSuperseded=false تُخفي المُستبدَل (لحساب آخر سعر)، وtrue تعيد التاريخ كله.
        /// </summary>
        Task<List<ReferencePrice>> GetGroupsAsync(IReadOnlyCollection<ReferencePrice> anchors, bool includeSuperseded);

        /// <summary>السجل الذي صحّح هذا السعر (إن وُجد)</summary>
        Task<ReferencePrice?> GetCorrectionOfAsync(int priceId);

        /// <summary>أسلاف السعر المعتمدون (سلسلة «يصحّح»)، ليصيروا مُستبدَلين عند اعتماد بديلهم</summary>
        Task<List<ReferencePrice>> GetApprovedAncestorsAsync(ReferencePrice price);

        Task<ReferencePrice> AddAsync(ReferencePrice price, AuditEntry audit);

        /// <summary>يضيف السجل البديل ويحدّث حال المُصحَّح (statusAfter) وأثر التصحيح معاً</summary>
        Task<ReferencePrice> CorrectAsync(ReferencePrice replacement, ReferencePrice original, ReferencePriceStatus statusAfter, byte[]? rowVersion, AuditEntry audit);

        /// <summary>اعتماد السعر وجعل أسلافه المعتمدين «مُستبدَلين» في معاملة واحدة</summary>
        Task ApproveAsync(ReferencePrice price, IReadOnlyCollection<ReferencePrice> supersededAncestors, byte[]? rowVersion, AuditEntry audit);

        /// <summary>أسعار الفرع غير المُستبدَلة عند الموردين وفي التواريخ المعطاة: مرشَّحو التكرار عند الاستيراد (المطابقة الدقيقة في القواعد)</summary>
        Task<List<ReferencePrice>> GetPossibleDuplicatesAsync(int branchId, IReadOnlyCollection<int> supplierIds, IReadOnlyCollection<DateTime> dates);

        /// <summary>يحفظ دفعة الاستيراد وكل أسعارها وأثرها في معاملة واحدة (الكل أو لا شيء)</summary>
        Task<ReferencePriceImport> ImportAsync(ReferencePriceImport batch, IReadOnlyCollection<ReferencePrice> prices, AuditEntry audit);

        /// <summary>إصدار وثيقة مثبَّت (مع وثيقته) للتحقق من مشروعه</summary>
        Task<ProjectDocumentVersion?> GetVersionAsync(int versionId);
    }
}
