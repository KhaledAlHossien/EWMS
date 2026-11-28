namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>
    /// إشعار الرد على أمر التكليف — رد واحد لكل تكليف ولا يُعاد (AssignmentId فريد).
    /// أجزاؤه (المباني، بنود التكلفة، الكادر، الموارد) تُحذف معه وتُستبدل كاملة عند التعديل.
    /// يوقّعه القسم المتابع ثم التخطيط وإدارة المشاريع. وهو جزءان (النموذج الورقي):
    /// جزء القسم المتابع (المباني، مساحة الموقع، التكلفة المبدئية وجدولها، المدير المقترح، التوصية)
    /// وجزء التخطيط وإدارة المشاريع (الموارد اللوجستية، الكوادر، المدة) — لكل جزء صاحبه الذي يعدّله ويوقّع عليه.
    /// </summary>
    public class StudyAssignmentResponse : IStudyDocument
    {
        public int Id { get; set; }

        /// <summary>رقم إشعار الرد (RSP-26-0001) من عدّاد الترقيم</summary>
        public string ResponseNumber { get; set; } = string.Empty;

        public int AssignmentId { get; set; }
        public StudyAssignment Assignment { get; set; } = null!;

        public int? ProposedManagerUserId { get; set; }
        public User? ProposedManager { get; set; }

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public DateTime ResponseDate { get; set; }
        public StudyRecommendation Recommendation { get; set; }
        /// <summary>مساحة الموقع العام — حقل واحد للرد كله (جزء القسم المتابع)</summary>
        public decimal? SiteArea { get; set; }
        /// <summary>ملاحظة بجانب مساحة الموقع العام (اختيارية)</summary>
        public string? SiteNote { get; set; }
        /// <summary>التكلفة المبدئية — حقل واحد للرد كله (جزء القسم المتابع)، بجانب مجموع الجدول التفصيلي</summary>
        public decimal? InitialCost { get; set; }
        /// <summary>الأرقام المالية في الرد بالدولار عند تفعيل هذا الحقل؛ الردود السابقة تحتفظ بقيمها.</summary>
        public bool CostsInUsd { get; set; }
        /// <summary>سعر الصرف المدخل يدوياً: ليرات سورية لكل دولار أمريكي.</summary>
        public decimal? UsdToSypRate { get; set; }
        /// <summary>المدة اللازمة بالأيام التقويمية (جزء التخطيط)</summary>
        public int? RequiredDays { get; set; }

        public StudyDocumentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public List<ResponseBuilding> Buildings { get; set; } = [];
        public List<ResponseCostItem> CostItems { get; set; } = [];
        public List<ResponseStaffEstimate> StaffEstimates { get; set; } = [];
        public List<ResponseLogisticItem> LogisticItems { get; set; } = [];

        public StudyStartOrder? StartOrder { get; set; }
        public StudyDiwanTransaction? DiwanTransaction { get; set; }
    }

    /// <summary>بيانات المباني: سطر لكل نوع بناء (المفتاح الرد + النوع ⇒ ثلاثة أسطر كحد أقصى)</summary>
    public class ResponseBuilding
    {
        public int ResponseId { get; set; }
        public StudyBuildingType BuildingType { get; set; }
        /// <summary>عدد أسطر التفاصيل (المباني المختلفة)، لا يشمل التكرار</summary>
        public int BuildingCount { get; set; }
        /// <summary>المساحة كاملة: مجموع (مساحة المبنى × عدد تكراراته)، يحسبها الخادم</summary>
        public decimal BuildingsArea { get; set; }
        public List<ResponseBuildingDetail> Details { get; set; } = [];
    }

    public class ResponseBuildingDetail
    {
        public int Id { get; set; }
        public int ResponseId { get; set; }
        public StudyBuildingType BuildingType { get; set; }
        public int SortOrder { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
        /// <summary>مساحة المبنى الواحد</summary>
        public decimal Area { get; set; }
        /// <summary>عدد تكرارات المبنى (1 على الأقل): المساحة كاملة = المساحة × التكرار، وكذلك كلفته</summary>
        public int Repetitions { get; set; } = 1;
        public string? Note { get; set; }
    }

    /// <summary>بند تكلفة — الإجمالي (الكمية × سعر الوحدة × التكرار) يُحسب ولا يُخزَّن</summary>
    public class ResponseCostItem
    {
        public int Id { get; set; }
        public int ResponseId { get; set; }
        public int SortOrder { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        /// <summary>عدد تكرارات مبنى البند (= تكرار سطر التفاصيل المقابل)</summary>
        public int Repetitions { get; set; } = 1;
    }

    /// <summary>الكادر المقترح: عدد المهندسين لكل اختصاص (اختصاص مرة واحدة لكل رد)</summary>
    public class ResponseStaffEstimate
    {
        public int ResponseId { get; set; }
        public int DisciplineId { get; set; }
        public EngineeringDiscipline Discipline { get; set; } = null!;
        public int StaffCount { get; set; }
    }

    /// <summary>مورد لوجستي مقترح (سيارة، جهاز، معدات…)</summary>
    public class ResponseLogisticItem
    {
        public int Id { get; set; }
        public int ResponseId { get; set; }
        public string Item { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Note { get; set; }
    }
}
