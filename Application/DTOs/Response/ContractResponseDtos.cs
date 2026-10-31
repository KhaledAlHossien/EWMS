namespace Application.DTOs.Response
{
    public class ContractorResponseDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? RegistrationNumber { get; set; }
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class EvaluationCriterionResponseDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Weight { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>سطر في قائمة العقود</summary>
    public class ContractListItemDto
    {
        public int Id { get; set; }
        /// <summary>الرقم الداخلي CON-…</summary>
        public string Number { get; set; } = string.Empty;
        /// <summary>الرقم الرسمي كما أُدخل</summary>
        public string? OfficialNumber { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int Method { get; set; }
        public string MethodAr { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public int? ContractorId { get; set; }
        public string? ContractorName { get; set; }
        public DateTime? ContractDate { get; set; }
        public decimal? Value { get; set; }
        /// <summary>القيمة بعد الملاحق المعتمدة (جمع بسيط)</summary>
        public decimal? EffectiveValue { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime? WarrantyEnd { get; set; }
        /// <summary>أيام باقية على نهاية الضمان (سالب بعد الانتهاء)</summary>
        public int? WarrantyDaysLeft { get; set; }
        /// <summary>ينتهي ضمانه قريباً (مرشّح متابعة، لا قرار)</summary>
        public bool WarrantyEndingSoon { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ContractOfferDto
    {
        public int Id { get; set; }
        public int ContractorId { get; set; }
        public string ContractorName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool IsAwarded { get; set; }
        public string RecordedByName { get; set; } = string.Empty;
        public bool CanAward { get; set; }
    }

    public class ContractAmendmentDto
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? ValueChange { get; set; }
        public int? DurationChangeDays { get; set; }
        public DateTime ApprovedAt { get; set; }
        public int? DocumentVersionId { get; set; }
        public string RecordedByName { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; }
    }

    public class CommitteeMemberDto
    {
        public int? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsExternal { get; set; }
        public string Capacity { get; set; } = string.Empty;
        public bool IsChair { get; set; }
    }

    public class CommitteeDto
    {
        public int Id { get; set; }
        public int Kind { get; set; }
        public string KindAr { get; set; } = string.Empty;
        public DateTime FormedAt { get; set; }
        public string? DecisionNumber { get; set; }
        public int? DecisionVersionId { get; set; }
        public string Recommendations { get; set; } = string.Empty;
        public List<CommitteeMemberDto> Members { get; set; } = [];
        public bool CanEdit { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class RetentionDecisionDto
    {
        public int Id { get; set; }
        public int Decision { get; set; }
        public string DecisionAr { get; set; } = string.Empty;
        public DateTime DecidedAt { get; set; }
        public string ApprovingAuthority { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public string RecordedByName { get; set; } = string.Empty;
    }

    public class RetentionEntryDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime WithheldAt { get; set; }
        public string Note { get; set; } = string.Empty;
        /// <summary>1 محتجز، 2 استمر احتجازه، 3 أُفرج عنه (من آخر قرار)</summary>
        public int State { get; set; }
        public string StateAr { get; set; } = string.Empty;
        public string RecordedByName { get; set; } = string.Empty;
        public List<RetentionDecisionDto> Decisions { get; set; } = [];
        public bool CanDecide { get; set; }
    }

    public class EvaluationScoreDto
    {
        public int CriterionId { get; set; }
        public string CriterionName { get; set; } = string.Empty;
        public decimal? Weight { get; set; }
        public decimal? Score { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class ContractEvaluationDto
    {
        public int Id { get; set; }
        public int ContractorId { get; set; }
        public string ContractorName { get; set; } = string.Empty;
        public int EvaluatedByUserId { get; set; }
        public string EvaluatedByName { get; set; } = string.Empty;
        public DateTime EvaluationDate { get; set; }
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string ReviewNote { get; set; } = string.Empty;
        public List<EvaluationScoreDto> Scores { get; set; } = [];
        public bool CanEdit { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanReview { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>تقييم في ملف المقاول مع عقده ومشروعه</summary>
    public class ContractorEvaluationHistoryDto : ContractEvaluationDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
    }

    public class ContractDetailDto : ContractListItemDto
    {
        public int? CorrespondenceId { get; set; }
        public string? CorrespondenceNumber { get; set; }
        public string? ExecutionOrderNumber { get; set; }
        public DateTime? ExecutionOrderDate { get; set; }
        public DateTime? StartDate { get; set; }
        public int? DurationDays { get; set; }
        public int? EffectiveDurationDays { get; set; }
        public string Scope { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public string? TenderNumber { get; set; }
        public DateTime? TenderSentAt { get; set; }
        public DateTime? TenderOpenedAt { get; set; }
        public DateTime? AwardedAt { get; set; }
        public string AwardNote { get; set; } = string.Empty;

        public int? WarrantyMonths { get; set; }
        public DateTime? WarrantyStart { get; set; }
        /// <summary>اقتراح نهاية الضمان من البداية والمدة (لا يُكتب تلقائياً)</summary>
        public DateTime? SuggestedWarrantyEnd { get; set; }
        /// <summary>مجموع المحتجز غير المفرج عنه (جمع بسيط لما أُدخل)</summary>
        public decimal StillWithheld { get; set; }

        public string CreatedByName { get; set; } = string.Empty;
        public byte[] RowVersion { get; set; } = [];

        // أعلام الأزرار: بالقواعد نفسها التي يفرضها الخادم
        public bool CanEdit { get; set; }
        /// <summary>الحالات التي يستطيع المستخدم نقل العقد إليها الآن</summary>
        public List<int> AllowedStatuses { get; set; } = [];
        public bool CanAddOffer { get; set; }
        public bool CanAddAmendment { get; set; }
        public bool CanManageCommittee { get; set; }
        public bool CanRecordRetention { get; set; }
        public bool CanEvaluate { get; set; }

        public List<ContractOfferDto> Offers { get; set; } = [];
        public List<ContractAmendmentDto> Amendments { get; set; } = [];
        public List<CommitteeDto> Committees { get; set; } = [];
        public List<RetentionEntryDto> Retention { get; set; } = [];
        public List<ContractEvaluationDto> Evaluations { get; set; } = [];
    }
}
