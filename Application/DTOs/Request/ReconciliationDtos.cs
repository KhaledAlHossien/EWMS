namespace Application.DTOs.Request
{
    /// <summary>بند مستند وارد: ما فيه، ومرجعه (سطر عرض مختار أو سعر مرجعي معتمد) إن وُجد</summary>
    public class ReconciliationLineRequestDto
    {
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        /// <summary>1 بلا مرجع، 2 سطر عرض مختار، 3 سعر مرجعي معتمد</summary>
        public int ReferenceKind { get; set; } = 1;
        public int? ReferenceId { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>تسجيل مستند وارد (فاتورة أو كشف) أو تعديل مسودته: البنود تُستبدل بالمرسلة، ومراجعها تُجمَّد عند الحفظ</summary>
    public class ReconciliationRequestDto
    {
        /// <summary>فرع المطابقة: فرع المشروع إن رُبط، وإلا فرع المستخدم (ولنظرة المؤسسة أي فرع)</summary>
        public int? BranchId { get; set; }
        /// <summary>1 فاتورة، 2 كشف</summary>
        public int DocumentKind { get; set; } = 1;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public int SupplierId { get; set; }
        public int? ProjectId { get; set; }
        public int? ContractId { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int? DocumentVersionId { get; set; }
        public List<ReconciliationLineRequestDto> Lines { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>تسجيل نتيجة التدقيق: 2 مطابق، 3 جزئياً، 4 فرق سعر، 5 فرق كمية، 6 بيانات غير مكتملة، 7 يحتاج مراجعة</summary>
    public class RecordReconciliationRequestDto
    {
        public int Result { get; set; }
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ReviewReconciliationRequestDto
    {
        public bool Approve { get; set; }
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ReferReconciliationRequestDto
    {
        public string Recipient { get; set; } = string.Empty;
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
