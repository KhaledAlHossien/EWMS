namespace Application.DTOs.Response
{
    /// <summary>سطر في قائمة المطابقات</summary>
    public class ReconciliationListItemDto
    {
        public int Id { get; set; }
        /// <summary>الرقم الداخلي MAT-…</summary>
        public string Number { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int DocumentKind { get; set; }
        public string DocumentKindAr { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int LinesCount { get; set; }
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public string PreparedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>بند مطابقة: ما في المستند ومرجعه المجمَّد والفروق التي يحسبها الخادم</summary>
    public class ReconciliationLineDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public int ReferenceKind { get; set; }
        public int? ReferenceOfferLineId { get; set; }
        public int? ReferencePriceId { get; set; }
        public string ReferenceLabel { get; set; } = string.Empty;
        public decimal? ReferenceQuantity { get; set; }
        public decimal? ReferenceUnitPrice { get; set; }
        public string? ReferenceUnit { get; set; }
        public string? ReferenceCurrency { get; set; }
        public bool HasReference { get; set; }
        /// <summary>بوحدة وعملة المرجع نفسهما فقط؛ وإلا يُشرح السبب في NotComparableReason</summary>
        public bool Comparable { get; set; }
        public string? NotComparableReason { get; set; }
        /// <summary>سعر المستند ناقص السعر المرجعي (موجب = أغلى من المرجع)</summary>
        public decimal? PriceDifference { get; set; }
        public decimal? QuantityDifference { get; set; }
        public bool HasDifference { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class ReconciliationDetailDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int DocumentKind { get; set; }
        public string DocumentKindAr { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public int? ContractId { get; set; }
        public string? ContractNumber { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public decimal Total { get; set; }
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        /// <summary>النتيجة التي سجّلها المدقق (تبقى بعد الاعتماد)</summary>
        public int? AuditResult { get; set; }
        public string? AuditResultAr { get; set; }
        public string AuditNote { get; set; } = string.Empty;
        public string PreparedByName { get; set; } = string.Empty;
        public DateTime? AuditedAt { get; set; }
        public string? DecidedByName { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecisionNote { get; set; } = string.Empty;
        public string? ReferredTo { get; set; }
        public string ReferralNote { get; set; } = string.Empty;
        public DateTime? ReferredAt { get; set; }
        public string? ReferredByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ReconciliationLineDto> Lines { get; set; } = [];
        /// <summary>النتيجة التي تقترحها المقارنة — معلومة لا قرار</summary>
        public int SuggestedResult { get; set; }
        public string SuggestedResultAr { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
        public bool CanRecord { get; set; }
        public bool CanDecide { get; set; }
        public bool CanRefer { get; set; }
        public bool CanCancel { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>مرجع مقترح لبند: سطر عرض مختار من طلب معتمد، أو سعر مرجعي معتمد للاستخدام الحالي</summary>
    public class ReconciliationCandidateDto
    {
        /// <summary>2 سطر عرض، 3 سعر مرجعي</summary>
        public int Kind { get; set; }
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public DateTime Date { get; set; }
    }
}
