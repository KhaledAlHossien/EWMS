namespace Application.DTOs.Response
{
    /// <summary>سطر في قائمة الأسعار المرجعية (وفي تاريخ المادة)</summary>
    public class ReferencePriceListItemDto
    {
        public int Id { get; set; }
        /// <summary>الرقم الداخلي REF-…</summary>
        public string Number { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string? ItemCode { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime PriceDate { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public int Source { get; set; }
        public string SourceAr { get; set; } = string.Empty;
        public string SourceDocument { get; set; } = string.Empty;
        /// <summary>1 تاريخي، 2 معتمد للاستخدام الحالي، 3 مُستبدَل بتصحيح</summary>
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        /// <summary>أحدث سعر مسجَّل (غير مُستبدَل) لهذه المادة بهذه الوحدة والعملة — معلومة لا اختيار</summary>
        public bool IsLatest { get; set; }
        /// <summary>أحدث سعر مسجَّل لهذا المورد لهذه المادة بهذه الوحدة والعملة</summary>
        public bool IsLatestForSupplier { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string? ApprovedByName { get; set; }
        public int? SupersedesId { get; set; }
    }

    /// <summary>تفصيل سعر مرجعي: بياناته الكاملة وسلسلة تصحيحه وتاريخ المادة (بالوحدة والعملة نفسهما) وأعلام الأزرار</summary>
    public class ReferencePriceDetailDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string? ItemCode { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Specifications { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime PriceDate { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public int Source { get; set; }
        public string SourceAr { get; set; } = string.Empty;
        public string SourceDocument { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public bool IsLatest { get; set; }
        public bool IsLatestForSupplier { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovalNote { get; set; } = string.Empty;
        public int? SupersedesId { get; set; }
        public string? SupersedesNumber { get; set; }
        public string CorrectionReason { get; set; } = string.Empty;
        /// <summary>السجل الذي حلّ محل هذا السعر (إن صُحِّح)</summary>
        public int? SupersededById { get; set; }
        public string? SupersededByNumber { get; set; }
        /// <summary>أسعار المادة نفسها بالوحدة والعملة نفسهما (كل الموردين، شاملة المُستبدَلة)، الأحدث تاريخاً أولاً — بلا إبراز للأعلى أو الأقل</summary>
        public List<ReferencePriceListItemDto> History { get; set; } = [];
        public bool CanCorrect { get; set; }
        public bool CanApprove { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
