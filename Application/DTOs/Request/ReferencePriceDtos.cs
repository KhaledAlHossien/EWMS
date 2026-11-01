namespace Application.DTOs.Request
{
    /// <summary>إضافة سعر مرجعي (سجل تاريخي لا يُعدَّل بعد حفظه؛ التصحيح بسجل جديد)</summary>
    public class ReferencePriceRequestDto
    {
        /// <summary>فرع السجل: فرع المشروع إن رُبط، وإلا فرع المستخدم (ولنظرة المؤسسة أي فرع)</summary>
        public int? BranchId { get; set; }
        public string? ItemCode { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Specifications { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime PriceDate { get; set; }
        public int? ProjectId { get; set; }
        /// <summary>1 يدوي، 2 عرض مورد أو مستند</summary>
        public int Source { get; set; } = 1;
        public string SourceDocument { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>تصحيح سعر: بيانات السجل البديل وسبب التصحيح ونسخة السعر المُصحَّح كما رآها المستخدم</summary>
    public class CorrectReferencePriceRequestDto
    {
        public string? ItemCode { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Specifications { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime PriceDate { get; set; }
        public int? ProjectId { get; set; }
        public int Source { get; set; } = 1;
        public string SourceDocument { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public string? Notes { get; set; }
        public string Reason { get; set; } = string.Empty;
        public byte[]? RowVersion { get; set; }
    }
}
