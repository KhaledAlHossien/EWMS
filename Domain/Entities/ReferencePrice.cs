namespace Domain.Entities
{
    /// <summary>
    /// حالة السعر المرجعي: «تاريخي» مسجَّل بمصدره وتاريخه ولا يُبنى عليه قرار، و«معتمد للاستخدام الحالي» اعتمده غير من أضافه،
    /// و«مُستبدَل» حلّ محله سجل تصحيح (يبقى ظاهراً في التاريخ ولا يُحذف). القيم لا تُعاد استعمالها.
    /// </summary>
    public enum ReferencePriceStatus
    {
        Historical = 1,
        ApprovedForCurrentUse = 2,
        Superseded = 3
    }

    /// <summary>مصدر السعر المرجعي: إدخال يدوي، أو عرض مورد/مستند، أو استيراد Excel (5ب.2)</summary>
    public enum ReferencePriceSource
    {
        Manual = 1,
        Offer = 2,
        Import = 3
    }

    /// <summary>
    /// سعر مرجعي لمادة أو منتج عند مورد في تاريخ معيّن (المرحلة 5ب.1). سجل تاريخي لا يُعدَّل سعره ولا يُحذف: التصحيح يُنشئ سجلاً جديداً
    /// يشير إلى المُصحَّح (<see cref="SupersedesPriceId"/>) فيصير المُصحَّح «مُستبدَلاً» ويبقى في التاريخ. لا مقارنة بين وحدات أو عملات مختلفة.
    /// </summary>
    public class ReferencePrice
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        /// <summary>رمز المادة (اختياري): إن وُجد فهو مفتاح تجميع الأسعار، وإلا اسمها</summary>
        public string? ItemCode { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Specifications { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        /// <summary>المورد من سجل مقاولي الفرع</summary>
        public int SupplierId { get; set; }
        public Contractor Supplier { get; set; } = null!;

        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "SYP";
        /// <summary>تاريخ السعر أو العرض (ما يُرتَّب به «آخر سعر»)</summary>
        public DateTime PriceDate { get; set; }

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        public ReferencePriceSource Source { get; set; } = ReferencePriceSource.Manual;
        /// <summary>رقم عرض السعر أو وصف المستند المصدر (مطلوب: لا سعر بلا مصدر)</summary>
        public string SourceDocument { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public ProjectDocumentVersion? DocumentVersion { get; set; }
        public string Notes { get; set; } = string.Empty;

        public ReferencePriceStatus Status { get; set; } = ReferencePriceStatus.Historical;
        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovalNote { get; set; } = string.Empty;

        /// <summary>السجل الذي يصحّحه هذا السجل (لا يتغير بعد الإنشاء)</summary>
        public int? SupersedesPriceId { get; set; }
        public ReferencePrice? SupersedesPrice { get; set; }
        public string CorrectionReason { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
