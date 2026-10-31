namespace Application.DTOs.Request
{
    public class ContractorRequestDto
    {
        /// <summary>فرع المقاول: فرع المستخدم، ولنظرة المؤسسة أي فرع</summary>
        public int? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? RegistrationNumber { get; set; }
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public byte[]? RowVersion { get; set; }
    }

    public class EvaluationCriterionRequestDto
    {
        /// <summary>فرع المعيار: فرع المستخدم، ولنظرة المؤسسة أي فرع</summary>
        public int? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>وزن اختياري (0–100)؛ لا يُحتسب منه أي تصنيف</summary>
        public decimal? Weight { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>فتح عقد أو تعديل بياناته. المبالغ والمدد وتواريخ الضمان تُدخَل كما اعتُمدت (لا معادلات). بعد سريان العقد تتجمّد الطبيعة والمتعاقد والقيمة والمدة.</summary>
    public class ContractRequestDto
    {
        public int ProjectId { get; set; }
        /// <summary>معاملة الديوان التي وصل بها أمر التنفيذ (اختياري)</summary>
        public int? CorrespondenceId { get; set; }
        public string? ExecutionOrderNumber { get; set; }
        public DateTime? ExecutionOrderDate { get; set; }
        /// <summary>ContractingMethod: 1 تنفيذ مباشر، 2 مناقصة، 3 عروض أسعار</summary>
        public int Method { get; set; } = 1;
        public int? ContractorId { get; set; }
        /// <summary>الرقم الرسمي للعقد (فريد داخل الفرع)</summary>
        public string? Number { get; set; }
        public DateTime? ContractDate { get; set; }
        public DateTime? StartDate { get; set; }
        public int? DurationDays { get; set; }
        public decimal? Value { get; set; }
        public string Currency { get; set; } = "SYP";
        public string? Scope { get; set; }
        public string? Notes { get; set; }

        public string? TenderNumber { get; set; }
        public DateTime? TenderSentAt { get; set; }
        public DateTime? TenderOpenedAt { get; set; }
        public DateTime? AwardedAt { get; set; }
        public string? AwardNote { get; set; }

        public int? WarrantyMonths { get; set; }
        public DateTime? WarrantyStart { get; set; }
        public DateTime? WarrantyEnd { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>تغيير حالة العقد بقرار: 2 ساري، 3 منجز، 4 مفسوخ، 5 ملغى (الفسخ والإلغاء يشترطان سبباً)</summary>
    public class ContractStatusRequestDto
    {
        public int Status { get; set; }
        public string? Reason { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ContractOfferRequestDto
    {
        public int ContractorId { get; set; }
        public decimal Amount { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>الإرساء على عرض بالقرار الرسمي: يحدّد المتعاقد معه، وتاريخ الإرساء وملاحظته للمناقصة</summary>
    public class AwardOfferRequestDto
    {
        public DateTime? AwardedAt { get; set; }
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ContractAmendmentRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? ValueChange { get; set; }
        public int? DurationChangeDays { get; set; }
        public DateTime ApprovedAt { get; set; }
        /// <summary>إصدار وثيقة الملحق من إضبارة المشروع (اختياري)</summary>
        public int? DocumentVersionId { get; set; }
    }

    public class CommitteeMemberRequestDto
    {
        public int? UserId { get; set; }
        public string? ExternalName { get; set; }
        public string Capacity { get; set; } = string.Empty;
        public bool IsChair { get; set; }
    }

    /// <summary>لجنة فنية ومالية: تشكيل جديدة، أو تعديل (يُستبدل أعضاؤها بالقائمة المرسلة)</summary>
    public class CommitteeRequestDto
    {
        public DateTime FormedAt { get; set; }
        public string? DecisionNumber { get; set; }
        public int? DecisionVersionId { get; set; }
        public string? Recommendations { get; set; }
        public List<CommitteeMemberRequestDto> Members { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    public class RetentionRequestDto
    {
        public decimal Amount { get; set; }
        public DateTime WithheldAt { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>قرار في مبلغ محتجز: 1 إفراج (نهائي)، 2 استمرار احتجاز، بجهة الاعتماد الرسمية</summary>
    public class RetentionDecisionRequestDto
    {
        public int Decision { get; set; }
        public DateTime DecidedAt { get; set; }
        public string ApprovingAuthority { get; set; } = string.Empty;
        public string? Note { get; set; }
        public int? DocumentVersionId { get; set; }
    }

    public class EvaluationScoreRequestDto
    {
        public int CriterionId { get; set; }
        /// <summary>درجة يدوية 0–100 (اختيارية)</summary>
        public decimal? Score { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>مسودة تقييم مقاول (جديدة أو معدَّلة: تُستبدل درجاتها بالقائمة المرسلة)</summary>
    public class ContractEvaluationRequestDto
    {
        public DateTime EvaluationDate { get; set; }
        public string? Notes { get; set; }
        public int? DocumentVersionId { get; set; }
        public List<EvaluationScoreRequestDto> Scores { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>عملية على تقييم (إرسال، مراجعة، إعادة): الملاحظة (مطلوبة عند الإعادة) وما رآه المستخدم</summary>
    public class EvaluationStepRequestDto
    {
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
