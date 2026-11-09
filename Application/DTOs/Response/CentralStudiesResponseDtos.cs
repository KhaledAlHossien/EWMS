namespace Application.DTOs.Response
{
    // ════════════════════ فرع الدراسات المركزية: المخرجات ════════════════════
    // كل تعداد يأتي رقماً مع اسمه العربي (…Name). أعلام Can… محسوبة لصلاحيات المستخدم الحالي وحالة السجل.

    public class StudyLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class StudyStageTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DefaultOrder { get; set; }
        public decimal DefaultWeight { get; set; }
        public int LinkedDocument { get; set; }
        public string LinkedDocumentName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    /// <summary>قيمة تعداد واسمها (لقوائم الواجهة)</summary>
    public record StudyEnumValueDto(int Value, string Name);

    /// <summary>
    /// موظف في قوائم اختيار فرع الدراسات (مدير المشروع، المهندسون، أعضاء الفريق) — موظف نشط من فرع الدراسة،
    /// بلا بريد ولا هاتف ولا رقم ذاتي ولا دور.
    /// </summary>
    public class StudyEmployeeDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public int? OfficeId { get; set; }
        public string? OfficeName { get; set; }
    }

    /// <summary>مكتب من مكاتب قسم مشارك (هدف «مكتب» في مهمة المرحلة)</summary>
    public record StudyOfficeOptionDto(int Id, string Name);

    /// <summary>سلسلة الدراسة من الطلب إلى المشروع — للتنقل بين المستندات</summary>
    public class StudyChainDto
    {
        public int RequestId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public int RequestStatus { get; set; }
        public int? AssignmentId { get; set; }
        public string? AssignmentNumber { get; set; }
        public int? AssignmentStatus { get; set; }
        public int? ResponseId { get; set; }
        public int? ResponseStatus { get; set; }
        public int? StartOrderId { get; set; }
        public string? StartOrderNumber { get; set; }
        public int? StartOrderStatus { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectCode { get; set; }
        public int? ProjectStatus { get; set; }
    }

    /// <summary>سطر توقيع على مستند</summary>
    public class StudyApprovalDto
    {
        public int Id { get; set; }
        public int SignerRole { get; set; }
        public string SignerRoleName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int Decision { get; set; }
        public string DecisionName { get; set; } = string.Empty;
        public int? SignerUserId { get; set; }
        public string? SignerName { get; set; }
        public string? JobTitle { get; set; }
        public bool HasSignature { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string? Note { get; set; }
        /// <summary>المستخدم الحالي يستطيع التوقيع على هذا السطر الآن</summary>
        public bool CanDecide { get; set; }
    }

    /// <summary>سطر في «بانتظار توقيعي»</summary>
    public class StudyApprovalQueueItemDto
    {
        public int ApprovalId { get; set; }
        public int OwnerType { get; set; }
        public string OwnerTypeName { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string OwnerNumber { get; set; } = string.Empty;
        public string OwnerTitle { get; set; } = string.Empty;
        public int SignerRole { get; set; }
        public string SignerRoleName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
    }

    /// <summary>أعلام الإجراءات المشتركة للمستندات</summary>
    public abstract class StudyDocumentDtoBase
    {
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        /// <summary>يُرسل للاعتماد (أو يُسجَّل نهائياً للطلب)</summary>
        public bool CanSubmit { get; set; }
        public bool CanCancel { get; set; }

        /// <summary>في التفاصيل فقط</summary>
        public StudyChainDto? Chain { get; set; }
        public List<StudyApprovalDto> Approvals { get; set; } = [];

        /// <summary>
        /// من يوقّع الآن (المستند بانتظار الاعتماد): حاملو صلاحية الصفة التالية ضمن حدّها، بلا المدير العام. null = لا ينطبق.
        /// قائمة فارغة = لا أحد يملك الصلاحية فيتعثّر المستند.
        /// </summary>
        public List<StudyPersonDto>? NextSigners { get; set; }
        /// <summary>العدد الكلي لمن يوقّع الآن (القائمة مقصوصة)</summary>
        public int NextSignersTotal { get; set; }
        /// <summary>من يصدر المستند التالي (المستند معتمد ولم يصدر التالي): حاملو صلاحية إصداره ضمن حدّها، بلا المدير العام. null = لا ينطبق</summary>
        public List<StudyPersonDto>? NextIssuers { get; set; }
        /// <summary>العدد الكلي لمن يصدر التالي (القائمة مقصوصة)</summary>
        public int NextIssuersTotal { get; set; }
    }

    /// <summary>موظف في «من يوقّع أو يصدر التالي»: الاسم والقسم والمسمّى فقط</summary>
    public class StudyPersonDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public string? JobTitle { get; set; }
    }

    public class StudyRequestDto : StudyDocumentDtoBase
    {
        public int Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public int Urgency { get; set; }
        public string UrgencyName { get; set; } = string.Empty;
        public string? DiwanRequestNumber { get; set; }
        public string? MinistryArchiveNumber { get; set; }
        public DateTime? MinistryArchiveDate { get; set; }
        public string? AdminArchiveNumber { get; set; }
        public DateTime? AdminArchiveDate { get; set; }
        public string? BeneficiaryArchiveNumber { get; set; }
        public DateTime? AddedDate { get; set; }
        public string? EntityCode { get; set; }
        public string MainBeneficiary { get; set; } = string.Empty;
        public string? SubBeneficiary { get; set; }
        public string GovernorateCode { get; set; } = string.Empty;
        public string GovernorateName { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? CityOrVillage { get; set; }
        public string? PropertyNature { get; set; }
        public int WorkType { get; set; }
        public string WorkTypeName { get; set; } = string.Empty;
        public string? FollowerName { get; set; }
        public string? FollowerPhone { get; set; }
        public string? Notes { get; set; }
        public int? SiteId { get; set; }
        public string? SiteName { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        /// <summary>يمكن إصدار تكليف له الآن (معتمد وبلا تكليف)</summary>
        public bool CanCreateAssignment { get; set; }
    }

    public class StudyAssignmentDto : StudyDocumentDtoBase
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string MainBeneficiary { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int FollowingDepartmentId { get; set; }
        public string FollowingDepartmentName { get; set; } = string.Empty;
        public string AssignmentNumber { get; set; } = string.Empty;
        public DateTime AssignmentDate { get; set; }
        public string? ProjectCode { get; set; }
        public decimal? AllocatedBudget { get; set; }
        public string ScopeSummary { get; set; } = string.Empty;
        public bool NeedsTechnicalEvaluation { get; set; }
        public bool NeedsResourceEstimate { get; set; }
        public bool NeedsPreliminaryBudget { get; set; }
        public bool NeedsFinalRecommendation { get; set; }
        public DateTime ReferralDate { get; set; }
        public DateTime ResponseDeadline { get; set; }
        public int Priority { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public bool CanCreateResponse { get; set; }
    }

    public class ResponseBuildingDto
    {
        public int BuildingType { get; set; }
        public string BuildingTypeName { get; set; } = string.Empty;
        public int BuildingCount { get; set; }
        public decimal BuildingsArea { get; set; }
    }

    public class ResponseCostItemDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public class ResponseStaffDto
    {
        public int DisciplineId { get; set; }
        public string DisciplineName { get; set; } = string.Empty;
        public int StaffCount { get; set; }
    }

    public class StudyItemDto
    {
        public int Id { get; set; }
        public string Item { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>إشعار الرد على التكليف</summary>
    public class StudyResponseDto : StudyDocumentDtoBase
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public string AssignmentNumber { get; set; } = string.Empty;
        public string? ProjectCode { get; set; }
        public int RequestId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int FollowingDepartmentId { get; set; }
        public string FollowingDepartmentName { get; set; } = string.Empty;
        public DateTime ResponseDate { get; set; }
        public int? ProposedManagerUserId { get; set; }
        public string? ProposedManagerName { get; set; }
        public int Recommendation { get; set; }
        public string RecommendationName { get; set; } = string.Empty;
        public int? RequiredDays { get; set; }
        public string ResponseNumber { get; set; } = string.Empty;
        public decimal? SiteArea { get; set; }
        /// <summary>التكلفة المبدئية المدخلة يدوياً (جزء القسم المتابع)</summary>
        public decimal? InitialCost { get; set; }
        /// <summary>مجموع جدول التكلفة التفصيلي (يُحسب)</summary>
        public decimal TotalCost { get; set; }
        /// <summary>يعدّل جزء القسم المتابع الآن (مسودة أو مرفوض، من قسم التكليف)</summary>
        public bool CanEditFollowingPart { get; set; }
        /// <summary>يعدّل جزء التخطيط الآن (وقع القسم المتابع وجاء دور التخطيط)</summary>
        public bool CanEditPlanningPart { get; set; }
        public List<ResponseBuildingDto> Buildings { get; set; } = [];
        public List<ResponseCostItemDto> CostItems { get; set; } = [];
        public List<ResponseStaffDto> StaffEstimates { get; set; } = [];
        public List<StudyItemDto> LogisticItems { get; set; } = [];
        public bool CanCreateStartOrder { get; set; }
    }

    public class StartOrderEngineerDto
    {
        public int EngineerUserId { get; set; }
        public string EngineerName { get; set; } = string.Empty;
        public int DisciplineId { get; set; }
        public string DisciplineName { get; set; } = string.Empty;
    }

    public class StudyStartOrderDto : StudyDocumentDtoBase
    {
        public int Id { get; set; }
        public int ResponseId { get; set; }
        public int AssignmentId { get; set; }
        public string AssignmentNumber { get; set; } = string.Empty;
        public string? ProjectCode { get; set; }
        public int RequestId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int CompetentDepartmentId { get; set; }
        public string CompetentDepartmentName { get; set; } = string.Empty;
        public int ProjectManagerUserId { get; set; }
        public string ProjectManagerName { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime StudyStartDate { get; set; }
        public DateTime StudyDeadline { get; set; }
        public List<StartOrderEngineerDto> Engineers { get; set; } = [];
        public List<StudyItemDto> Resources { get; set; } = [];
        public bool CanCreateProject { get; set; }
    }

    public class ProjectMemberDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? RoleTitle { get; set; }
        public string AssignedByName { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
    }

    public class ProjectDepartmentDto
    {
        public int Id { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string AddedByName { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
        public List<ProjectMemberDto> Members { get; set; } = [];
        /// <summary>مكاتب القسم (لاختيار هدف «مكتب» في المهمة)</summary>
        public List<StudyOfficeOptionDto> Offices { get; set; } = [];
        /// <summary>المستخدم الحالي يعيّن أعضاء هذا القسم (رئيسه)</summary>
        public bool CanManageMembers { get; set; }
    }

    public class ProjectStageDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int StageTypeId { get; set; }
        public string StageTypeName { get; set; } = string.Empty;
        public int LinkedDocument { get; set; }
        public int SortOrder { get; set; }
        public decimal Weight { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public decimal Progress { get; set; }
        public int? ResponsibleDepartmentId { get; set; }
        public string? ResponsibleDepartmentName { get; set; }
        public string? ClosedByName { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int TasksTotal { get; set; }
        public int TasksDone { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class StudyProjectDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int StartOrderId { get; set; }
        public string StartOrderNumber { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        /// <summary>موقع المشروع في سجل المواقع (منسوخ من الطلب، قد يكون فارغاً) — منه تظهر مشاريع الفرع على الخريطة</summary>
        public int? SiteId { get; set; }
        public int ManagerUserId { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public DateTime PlannedStartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public decimal Progress { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanCancel { get; set; }
        /// <summary>كل المراحل مغلقة ولا طلب إنجاز معلّق</summary>
        public bool CanRequestCompletion { get; set; }

        /// <summary>في التفاصيل فقط</summary>
        public StudyChainDto? Chain { get; set; }
        public List<ProjectDepartmentDto> Departments { get; set; } = [];
        public List<ProjectStageDto> Stages { get; set; } = [];
        public List<StudyApprovalDto> Approvals { get; set; } = [];
    }

    public class StageTaskDto
    {
        public int Id { get; set; }
        public int StageId { get; set; }
        public string StageName { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public int TargetType { get; set; }
        public string TargetTypeName { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public int? OfficeId { get; set; }
        public int? AssigneeUserId { get; set; }
        public string AssignedByName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public decimal Progress { get; set; }
        public decimal Weight { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public bool CanEdit { get; set; }
        public bool CanUpdateProgress { get; set; }
    }

    public class DesignApprovalDto
    {
        public int Id { get; set; }
        public int VersionId { get; set; }
        public string VersionLabel { get; set; } = string.Empty;
        public int DrawingId { get; set; }
        public string DrawingName { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string CdaNumber { get; set; } = string.Empty;
        public DateTime CdaDate { get; set; }
        public string? Content { get; set; }
        public string? TechnicalNotes { get; set; }
        public int Decision { get; set; }
        public string DecisionName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
        /// <summary>يُعدَّل ويُحذف قبل أول توقيع فقط</summary>
        public bool CanEdit { get; set; }
        public List<StudyApprovalDto> Approvals { get; set; } = [];
    }

    public class ArchitecturalVersionDto
    {
        public int Id { get; set; }
        public int DrawingId { get; set; }
        public int VersionNumber { get; set; }
        public string Label { get; set; } = string.Empty;
        public DateTime VersionDate { get; set; }
        public string? Content { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public int? DesignApprovalId { get; set; }
        public string? DesignApprovalNumber { get; set; }
        public int? DesignApprovalDecision { get; set; }
    }

    public class StudyDrawingDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public int Kind { get; set; }
        public string KindName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DrawingNumber { get; set; } = string.Empty;
        public DateTime DrawingDate { get; set; }
        public string? Description { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int? OfficeId { get; set; }
        public string? OfficeName { get; set; }
        public int PreparedByUserId { get; set; }
        public string PreparedByName { get; set; } = string.Empty;
        public int? BasedOnVersionId { get; set; }
        public string? BasedOnVersionLabel { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public bool CanEdit { get; set; }
        public bool CanReview { get; set; }
        /// <summary>للمعماري فقط (في التفاصيل)</summary>
        public List<ArchitecturalVersionDto> Versions { get; set; } = [];
    }

    public class StudyAttachmentDto
    {
        public int Id { get; set; }
        public int OwnerType { get; set; }
        public int OwnerId { get; set; }
        public int AttachmentTypeId { get; set; }
        public string AttachmentTypeName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        /// <summary>بالبايت</summary>
        public long Size { get; set; }
        public bool Previewable { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public bool CanDelete { get; set; }
    }

    public record StudyAttachmentContentDto(string FileName, string ContentType, Stream Content, bool Previewable);

    public class StudyActivityLogDto
    {
        public int Id { get; set; }
        public int EntityType { get; set; }
        public string EntityTypeName { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public int Action { get; set; }
        public string ActionName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Details { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>توقيع في نموذج مطبوع: الصورة هي نسخة التوقيع المحفوظة وقت القرار (Data URL)، وnull تعني مربعاً فارغاً</summary>
    public class StudyPrintSignatureDto
    {
        public int SignerRole { get; set; }
        public string SignerRoleName { get; set; } = string.Empty;
        public int Decision { get; set; }
        public string DecisionName { get; set; } = string.Empty;
        public string? SignerName { get; set; }
        public string? JobTitle { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string? DecidedAtHijri { get; set; }
        public string? Note { get; set; }
        public string? Image { get; set; }
    }

    /// <summary>نموذج ورقي للطباعة: ترويسة (الرقم، فرع الدراسة، التاريخ الميلادي والهجري) + المستند كاملاً + التواقيع. يُملأ واحد فقط من المستندات الخمسة</summary>
    public class StudyPrintDto
    {
        public string DocumentType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        /// <summary>التاريخ الهجري yyyy/MM/dd بقاعدة طباعة الإجازة (VacationRules.HijriDayOffset)</summary>
        public string DocumentDateHijri { get; set; } = string.Empty;
        /// <summary>CDA فقط: رقم أمر بدء المشروع</summary>
        public string? StartOrderNumber { get; set; }
        public StudyRequestDto? Request { get; set; }
        public StudyAssignmentDto? Assignment { get; set; }
        public StudyResponseDto? Response { get; set; }
        public StudyStartOrderDto? StartOrder { get; set; }
        public DesignApprovalDto? DesignApproval { get; set; }
        public List<StudyPrintSignatureDto> Signatures { get; set; } = [];
    }
}
