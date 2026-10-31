namespace Application.DTOs.Request
{
    public class PricingItemRequestDto
    {
        public string Description { get; set; } = string.Empty;
        public string? Specifications { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    /// <summary>تسجيل طلب تسعير أو تعديله. البنود تُستبدل بالقائمة المرسلة وتتجمّد بعد أول عرض.</summary>
    public class PricingRequestRequestDto
    {
        public int? ProjectId { get; set; }
        public int? ContractId { get; set; }
        /// <summary>المعاملة الواردة التي جاء بها الطلب (اختياري)</summary>
        public int? CorrespondenceId { get; set; }
        /// <summary>فرع الطلب عند عدم الربط بمشروع: فرع المستخدم، ولنظرة المؤسسة أي فرع</summary>
        public int? BranchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool OutsideContractScope { get; set; }
        public DateTime? DueDate { get; set; }
        public List<PricingItemRequestDto> Items { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>تكليف موظف المكتب (أو تغييره)</summary>
    public class AssignPricingRequestDto
    {
        public int AssigneeUserId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>عملية على طلب بلا بيانات إضافية (إلغاء…): ما رآه المستخدم</summary>
    public class PricingStepRequestDto
    {
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class PricingLineRequestDto
    {
        public int ItemId { get; set; }
        public decimal UnitPrice { get; set; }
        /// <summary>الوحدة المعروضة (تخالف وحدة البند فيُعرض «غير قابل للمقارنة»)</summary>
        public string Unit { get; set; } = string.Empty;
        /// <summary>ConformityResult: 1 لم تُقيَّم، 2 مطابق، 3 غير مطابق</summary>
        public int Conformity { get; set; } = 1;
        public string? ConformityNote { get; set; }
    }

    /// <summary>عرض سعر مستلم: عملة واحدة لكل بنوده. عند التعديل يُستبدل ما أُرسل من البنود (والسطر المختار لا يُغيَّر)</summary>
    public class PricingOfferRequestDto
    {
        public int SupplierId { get; set; }
        public string? OfferNumber { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string Currency { get; set; } = "SYP";
        public string? SupplyTerms { get; set; }
        public string? Note { get; set; }
        public int? DocumentVersionId { get; set; }
        public List<PricingLineRequestDto> Lines { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>اختيار سطر لبند: بسبب مكتوب (الاختيار قرار لا حساب)</summary>
    public class SelectPricingLineRequestDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>القرار على التوصية: 1 اعتماد، 2 رفض، 3 إعادة لجمع العروض (الرفض والإعادة بسبب)</summary>
    public class DecidePricingRequestDto
    {
        public int Decision { get; set; }
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>تسجيل إرسال النتيجة إلى الجهة المختصة</summary>
    public class SendPricingResultRequestDto
    {
        public string Recipient { get; set; } = string.Empty;
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class AgreementLineRequestDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
    }

    /// <summary>إعداد محضر اتفاق مع متعهد جديد أو تعديله (يُستبدل ما أُرسل من بنوده)</summary>
    public class PricingAgreementRequestDto
    {
        public int? BranchId { get; set; }
        public int SupplierId { get; set; }
        public string? Number { get; set; }
        public DateTime AgreementDate { get; set; }
        public int? ProjectId { get; set; }
        public int? ContractId { get; set; }
        public int? PricingRequestId { get; set; }
        public string? TechnicalSpecs { get; set; }
        public string? ExecutionMethod { get; set; }
        public string Currency { get; set; } = "SYP";
        public string? Notes { get; set; }
        public int? DocumentVersionId { get; set; }
        public List<AgreementLineRequestDto> Lines { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>اعتماد المحضر أو إعادته للتصحيح (الإعادة بسبب)</summary>
    public class ReviewPricingAgreementRequestDto
    {
        public bool Approve { get; set; }
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
