namespace Application.DTOs.Request
{
    // ════════════════════ فرع الدراسات المركزية: نماذج الإدخال ════════════════════
    // القيم الرقمية للتعدادات كما في Domain.Entities.CentralStudiesBranch (وقوائمها في GET api/StudyLookups/Enums).
    // RowVersion اختياري في التعديل: إن أُرسل يُرفض الحفظ إذا عدّل غيرك السجل بعد أن فتحته.

    /// <summary>نوع مرحلة</summary>
    public class StudyStageTypeFormDto
    {
        public string Name { get; set; } = string.Empty;
        public int DefaultOrder { get; set; }
        public decimal DefaultWeight { get; set; }
        /// <summary>0 لا شيء، 1 الطلب، 2 التكليف، 3 الرد، 4 أمر البدء</summary>
        public int LinkedDocument { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>عنصر قائمة بسيطة: نوع مرفق أو اختصاص هندسي</summary>
    public class StudyLookupFormDto
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>بحث المستندات: الحالة، نص (الرقم أو الجهة أو الكود)، الفرع (لنظرة المؤسسة)، الفترة</summary>
    public class StudyDocumentFilterDto
    {
        public int? Status { get; set; }
        public string? Search { get; set; }
        public int? BranchId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class StudyCancelDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>طلب تجهيز دراسة تكاليف</summary>
    public class StudyRequestFormDto
    {
        public DateTime RequestDate { get; set; }
        /// <summary>1 مستعجل، 2 عادي</summary>
        public int Urgency { get; set; }
        /// <summary>رقم الطلب من الديوان المركزي (يدوي، اختياري)</summary>
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
        /// <summary>رمز المحافظة SY01…SY14</summary>
        public string GovernorateCode { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? CityOrVillage { get; set; }
        public string? PropertyNature { get; set; }
        /// <summary>1 إنشاء، 2 صيانة وترميم</summary>
        public int WorkType { get; set; }
        public string? FollowerName { get; set; }
        public string? FollowerPhone { get; set; }
        public string? Notes { get; set; }
        /// <summary>اختياري: الموقع من سجل المواقع المشترك</summary>
        public int? SiteId { get; set; }
        /// <summary>عند الإنشاء ولنظرة المؤسسة فقط: فرع الطلب (غيرهم يُسجَّل طلبه في فرعه)</summary>
        public int? BranchId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>التكليف الداخلي (IPA) — RequestId يُقرأ عند الإنشاء فقط</summary>
    public class StudyAssignmentFormDto
    {
        public int RequestId { get; set; }
        public int FollowingDepartmentId { get; set; }
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
        /// <summary>1 عاجل جداً وسري، 2 عاجل، 3 عادي</summary>
        public int Priority { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>إشعار الرد — AssignmentId عند الإنشاء فقط. الأجزاء تُستبدل كاملة عند كل حفظ</summary>
    /// <summary>جزء القسم المتابع من إشعار الرد (الإنشاء والتعديل): يملؤه موظفو القسم المتابع للتكليف</summary>
    public class StudyResponseFormDto
    {
        public int AssignmentId { get; set; }
        public DateTime ResponseDate { get; set; }
        public int? ProposedManagerUserId { get; set; }
        /// <summary>1 قبول، 2 تعديل، 3 غير متاح</summary>
        public int Recommendation { get; set; }
        /// <summary>مساحة الموقع العام (حقل واحد للرد)</summary>
        public decimal? SiteArea { get; set; }
        /// <summary>التكلفة المبدئية (حقل واحد للرد، بجانب جدول البنود)</summary>
        public decimal? InitialCost { get; set; }
        public List<ResponseBuildingFormDto> Buildings { get; set; } = [];
        public List<ResponseCostItemFormDto> CostItems { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>جزء التخطيط وإدارة المشاريع (PUT UpdatePlanning): الكوادر والموارد اللوجستية والمدة</summary>
    public class StudyResponsePlanningFormDto
    {
        public int? RequiredDays { get; set; }
        public List<ResponseStaffFormDto> StaffEstimates { get; set; } = [];
        public List<StudyItemFormDto> LogisticItems { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    public class ResponseBuildingFormDto
    {
        /// <summary>1 إعادة تأهيل، 2 إنشاء جديد (سطر واحد لكل نوع)</summary>
        public int BuildingType { get; set; }
        public int BuildingCount { get; set; }
        public decimal BuildingsArea { get; set; }
    }

    public class ResponseCostItemFormDto
    {
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class ResponseStaffFormDto
    {
        public int DisciplineId { get; set; }
        public int StaffCount { get; set; }
    }

    /// <summary>بند مورد: لوجستي مقترح في الرد، أو مخصص في أمر البدء</summary>
    public class StudyItemFormDto
    {
        public string Item { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>أمر بدء الدراسة (PIO) — ResponseId عند الإنشاء فقط</summary>
    public class StudyStartOrderFormDto
    {
        public int ResponseId { get; set; }
        public int CompetentDepartmentId { get; set; }
        public int ProjectManagerUserId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime StudyStartDate { get; set; }
        public DateTime StudyDeadline { get; set; }
        public List<StartOrderEngineerFormDto> Engineers { get; set; } = [];
        public List<StudyItemFormDto> Resources { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    public class StartOrderEngineerFormDto
    {
        public int EngineerUserId { get; set; }
        public int DisciplineId { get; set; }
    }

    /// <summary>قرار موقّع: 2 معتمد، 3 معتمد مع ملاحظات (الملاحظة مطلوبة)، 4 مرفوض (السبب مطلوب)</summary>
    public class StudyDecisionDto
    {
        public int Decision { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>المشروع — StartOrderId و Code عند الإنشاء فقط (الكود يُنسخ من التكليف، ويُطلب هنا إن خلا التكليف منه)</summary>
    public class StudyProjectFormDto
    {
        public int StartOrderId { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime PlannedStartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        /// <summary>عند التعديل فقط: مدير المشروع (الافتراضي عند الإنشاء مدير أمر البدء)</summary>
        public int? ManagerUserId { get; set; }
        /// <summary>عند التعديل فقط: 1 جارٍ أو 2 معلّق (الإكمال بالاعتماد والإلغاء بـ Cancel)</summary>
        public int? Status { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ProjectDepartmentFormDto
    {
        public int DepartmentId { get; set; }
    }

    public class ProjectMemberFormDto
    {
        public int UserId { get; set; }
        public string? RoleTitle { get; set; }
    }

    /// <summary>مرحلة — ProjectId و StageTypeId عند الإنشاء فقط</summary>
    public class StudyStageFormDto
    {
        public int ProjectId { get; set; }
        public int StageTypeId { get; set; }
        public decimal Weight { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public int? ResponsibleDepartmentId { get; set; }
        /// <summary>عند التعديل فقط: 1 لم تبدأ أو 2 جارية (الإغلاق بـ Close)</summary>
        public int? Status { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ReorderStudyStagesDto
    {
        /// <summary>كل مراحل المشروع بالترتيب الجديد</summary>
        public List<int> StageIds { get; set; } = [];
    }

    /// <summary>مهمة مرحلة — StageId عند الإنشاء فقط. يُملأ عمود الإسناد المطابق لـ TargetType فقط</summary>
    public class StudyTaskFormDto
    {
        public int StageId { get; set; }
        /// <summary>1 قسم مشارك، 2 مكتب من قسم مشارك، 3 عضو في فريق المشروع</summary>
        public int TargetType { get; set; }
        public int? DepartmentId { get; set; }
        public int? OfficeId { get; set; }
        public int? AssigneeUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public decimal Weight { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>تحديث الإنجاز من المكلَّف: النسبة 0–100 والحالة (3 = منجزة تجعل النسبة 100)</summary>
    public class StudyTaskProgressDto
    {
        public decimal Progress { get; set; }
        public int Status { get; set; }
    }

    /// <summary>مخطط — ProjectId و Kind عند الإنشاء فقط (1 معماري، 2 إنشائي، 3 كهرباء وميكانيك، 4 داخلي، 5 تقني)</summary>
    public class StudyDrawingFormDto
    {
        public int ProjectId { get; set; }
        public int Kind { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DrawingNumber { get; set; } = string.Empty;
        public DateTime DrawingDate { get; set; }
        public string? Description { get; set; }
        public int? OfficeId { get; set; }
        /// <summary>عند الإنشاء ولنظرة المؤسسة فقط: القسم المُصدِر (غيرهم يصدر المخطط باسم قسمه)</summary>
        public int? DepartmentId { get; set; }
        /// <summary>لغير المعماري: نسخة معمارية معتمدة من المشروع نفسه</summary>
        public int? BasedOnVersionId { get; set; }
        /// <summary>عند التعديل فقط: 1 قيد الإعداد أو 2 بانتظار المراجعة (الاعتماد بالمراجعة أو CDA)</summary>
        public int? Status { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class StudyDrawingReviewDto
    {
        public bool Approve { get; set; }
        public string? Note { get; set; }
    }

    public class ArchitecturalVersionFormDto
    {
        public DateTime VersionDate { get; set; }
        public string? Content { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>اعتماد المخططات الأولي (CDA) — VersionId عند الإنشاء فقط</summary>
    public class DesignApprovalFormDto
    {
        public int VersionId { get; set; }
        public DateTime CdaDate { get; set; }
        public string? Content { get; set; }
        public string? TechnicalNotes { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
