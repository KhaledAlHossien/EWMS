namespace Domain.Entities
{
    /// <summary>نوع المستند الوارد من القسم المالي: فاتورة أو كشف</summary>
    public enum ReconciliationDocumentKind
    {
        Invoice = 1,
        Statement = 2
    }

    /// <summary>
    /// حالة مطابقة المستند (الثماني المعتمدة 2026-11-01 + المسودة والإلغاء). المدقق يسجّل إحدى الست الأولى (2–7)، ويقرر غيره
    /// «معتمد بعد التدقيق» أو «معاد للتصحيح». القيم لا تُعاد استعمالها.
    /// </summary>
    public enum ReconciliationStatus
    {
        Draft = 1,
        Match = 2,                 // مطابق
        PartialMatch = 3,          // مطابق جزئياً
        PriceDifference = 4,       // يوجد فرق في السعر
        QuantityDifference = 5,    // يوجد فرق في الكمية
        IncompleteData = 6,        // بيانات غير مكتملة
        NeedsReview = 7,           // يحتاج إلى مراجعة
        ApprovedAfterAudit = 8,    // معتمد بعد التدقيق
        ReturnedForCorrection = 9, // معاد للتصحيح
        Cancelled = 10             // ملغى (بديل الحذف)
    }

    /// <summary>مصدر السعر المرجعي لبند المطابقة</summary>
    public enum ReconciliationReferenceKind
    {
        None = 1,
        OfferLine = 2,       // سطر مختار من عرض في طلب تسعير
        ReferencePrice = 3   // سعر مرجعي معتمد للاستخدام الحالي
    }

    /// <summary>
    /// مطابقة فاتورة أو كشف وارد مع الأسعار المعتمدة أو المرجعية (المرحلة 5ب.3). تُوثَّق الفروق في الكمية والسعر بدل الحكم بمطابق/غير مطابق فقط.
    /// النظام يقارن ويقترح ولا يقرر؛ ومن دقّق لا يعتمد؛ والمعتمد لا يُعدَّل ([ADR-0007], [ADR-0008]).
    /// </summary>
    public class PriceReconciliation
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public ReconciliationDocumentKind DocumentKind { get; set; } = ReconciliationDocumentKind.Invoice;
        /// <summary>رقم الفاتورة أو الكشف (فريد لكل مورد ونوع داخل الفرع)</summary>
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }

        public int SupplierId { get; set; }
        public Contractor Supplier { get; set; } = null!;
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }
        public int? ContractId { get; set; }
        public Contract? Contract { get; set; }

        /// <summary>عملة المستند (واحدة)</summary>
        public string Currency { get; set; } = "SYP";
        public string Notes { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public ProjectDocumentVersion? DocumentVersion { get; set; }

        public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Draft;
        /// <summary>النتيجة التي سجّلها المدقق (تبقى بعد الاعتماد أو الإعادة)</summary>
        public ReconciliationStatus? AuditResult { get; set; }
        public string AuditNote { get; set; } = string.Empty;

        public int PreparedByUserId { get; set; }
        public User PreparedByUser { get; set; } = null!;
        public DateTime? AuditedAt { get; set; }

        public int? DecidedByUserId { get; set; }
        public User? DecidedByUser { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecisionNote { get; set; } = string.Empty;

        /// <summary>الإحالة إلى الجهة المختصة بعد الاعتماد (تسجيل يدوي)</summary>
        public string? ReferredTo { get; set; }
        public string ReferralNote { get; set; } = string.Empty;
        public DateTime? ReferredAt { get; set; }
        public int? ReferredByUserId { get; set; }
        public User? ReferredByUser { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<PriceReconciliationLine> Lines { get; set; } = new List<PriceReconciliationLine>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>بند مطابقة: ما في المستند (كمية وسعر وحدة) وما رُبط به من مرجع مجمَّد (يُسجَّل كما كان عند الربط)</summary>
    public class PriceReconciliationLine
    {
        public int Id { get; set; }
        public int PriceReconciliationId { get; set; }
        public PriceReconciliation PriceReconciliation { get; set; } = null!;
        public int SortOrder { get; set; }

        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal InvoiceQuantity { get; set; }
        public decimal InvoiceUnitPrice { get; set; }

        public ReconciliationReferenceKind ReferenceKind { get; set; } = ReconciliationReferenceKind.None;
        public int? ReferenceOfferLineId { get; set; }
        public PricingOfferLine? ReferenceOfferLine { get; set; }
        public int? ReferencePriceId { get; set; }
        public ReferencePrice? ReferencePrice { get; set; }
        /// <summary>وصف المرجع المجمَّد، مثل «PRC-2026-00012 — مورد»</summary>
        public string ReferenceLabel { get; set; } = string.Empty;
        public decimal? ReferenceQuantity { get; set; }
        public decimal? ReferenceUnitPrice { get; set; }
        public string? ReferenceUnit { get; set; }
        public string? ReferenceCurrency { get; set; }

        public string Note { get; set; } = string.Empty;
    }
}
