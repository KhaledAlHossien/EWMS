namespace Domain.Entities
{
    /// <summary>
    /// حالة طلب التسعير (2026-10-31، المرحلة 5أ): مسجَّل ← قيد جمع العروض (بعد التكليف) ← موصى به ← معتمد أو مرفوض ← أُرسلت النتيجة؛ أو ملغى.
    /// «الإعادة» من المعتمِد ترجع الطلب إلى جمع العروض. تنتقل الحالة بفعل مخوَّل لا بمرور الوقت. القيم لا تُعاد استعمالها.
    /// </summary>
    public enum PricingRequestStatus
    {
        Registered = 1,   // مسجَّل: بانتظار التكليف
        Collecting = 2,   // قيد جمع العروض ومقارنتها
        Recommended = 3,  // وُثّقت التوصية وتنتظر الاعتماد النهائي
        Approved = 4,     // اعتُمدت النتيجة
        Rejected = 5,     // رُفضت
        ResultSent = 6,   // أُرسلت النتيجة إلى الجهة المختصة
        Cancelled = 7     // ملغى (بديل الحذف)
    }

    /// <summary>نتيجة المطابقة الفنية لبند في عرض: لا يُختار سطر لم تتحقق مطابقته ولو كان الأرخص. القيم لا تُعاد استعمالها.</summary>
    public enum ConformityResult
    {
        NotEvaluated = 1,
        Conforming = 2,
        NonConforming = 3
    }

    /// <summary>
    /// حالة محضر الاتفاق مع متعهد جديد: مسودة ← مرسل للاعتماد ← معتمد (لا يُعدَّل) أو معاد للتصحيح؛ أو ملغى.
    /// يُعدّه مدير مكتب التسعير وحده (صلاحية مستقلة) ويعتمده شخص آخر.
    /// </summary>
    public enum AgreementStatus
    {
        Draft = 1,
        Submitted = 2,
        Approved = 3,
        Returned = 4,
        Cancelled = 5
    }

    /// <summary>
    /// طلب تسعير وارد إلى مكتب التسعير، منه ما يقدّمه مهندس الموقع لأعمال أو مواد خارج العقد. يخص فرعاً واحداً، ويُربط اختيارياً بمشروع وعقد ومعاملة
    /// الديوان. بنوده كميات ووحدات ومواصفات، وعروضه من الموردين تُقارَن بنداً بنداً. لا يُحذف: يُلغى. الرقم الداخلي PRC-{سنة}-{id} يُحسب.
    /// </summary>
    public class PricingRequest
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }
        public int? ContractId { get; set; }
        public Contract? Contract { get; set; }
        /// <summary>المعاملة الواردة التي جاء بها الطلب (اختياري)</summary>
        public int? CorrespondenceId { get; set; }
        public Correspondence? Correspondence { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>أعمال أو مواد خارج نطاق العقد (يحدّده مقدّم الطلب)</summary>
        public bool OutsideContractScope { get; set; }
        /// <summary>الموعد المطلوب (تجاوزه تنبيه لا قرار)</summary>
        public DateTime? DueDate { get; set; }
        public PricingRequestStatus Status { get; set; } = PricingRequestStatus.Registered;

        public int RegisteredByUserId { get; set; }
        public User RegisteredByUser { get; set; } = null!;

        /// <summary>موظف المكتب المكلَّف بالمتابعة (يُكلِّفه من يملك التكليف؛ يعمل على العروض)</summary>
        public int? AssigneeUserId { get; set; }
        public User? Assignee { get; set; }
        public DateTime? AssignedAt { get; set; }
        public int? AssignedByUserId { get; set; }
        public User? AssignedByUser { get; set; }

        /// <summary>التوصية والاختيار الموثَّقان (من أعدّهما لا يعتمد)</summary>
        public string RecommendationNote { get; set; } = string.Empty;
        public int? RecommendedByUserId { get; set; }
        public User? RecommendedByUser { get; set; }
        public DateTime? RecommendedAt { get; set; }

        public int? DecidedByUserId { get; set; }
        public User? DecidedByUser { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecisionNote { get; set; } = string.Empty;

        /// <summary>الجهة التي أُرسلت إليها النتيجة (مهندس الموقع، مكتب العقود…)</summary>
        public string? ResultRecipient { get; set; }
        public DateTime? ResultSentAt { get; set; }
        public int? ResultSentByUserId { get; set; }
        public User? ResultSentByUser { get; set; }
        public string ResultNote { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        /// <summary>موعد الطلب الذي نُبّه عن تجاوزه (مرة لكل موعد)</summary>
        public DateTime? OverdueNotifiedFor { get; set; }

        public ICollection<PricingRequestItem> Items { get; set; } = new List<PricingRequestItem>();
        public ICollection<PricingOffer> Offers { get; set; } = new List<PricingOffer>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>بند في طلب التسعير: مادة أو عمل بمواصفاته وكميته ووحدته. لا مقارنة بين وحدات مختلفة بلا تحويل معتمد.</summary>
    public class PricingRequestItem
    {
        public int Id { get; set; }
        public int PricingRequestId { get; set; }
        public PricingRequest PricingRequest { get; set; } = null!;
        public int SortOrder { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Specifications { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    /// <summary>
    /// عرض سعر مستلم من مورد أو متعهد (من سجل مقاولي الفرع) بعملة واحدة لكل بنوده. سجل لا يُحذف بعد تسجيله، ويُقارَن بنداً بنداً.
    /// مورد واحد لكل طلب. الوثيقة الداعمة إصدار مثبَّت من إضبارة مشروع الطلب (إن ارتبط بمشروع).
    /// </summary>
    public class PricingOffer
    {
        public int Id { get; set; }
        public int PricingRequestId { get; set; }
        public PricingRequest PricingRequest { get; set; } = null!;
        public int SupplierId { get; set; }
        public Contractor Supplier { get; set; } = null!;

        /// <summary>رقم عرض المورد كما ورد (اختياري)</summary>
        public string? OfferNumber { get; set; }
        public DateTime ReceivedAt { get; set; }
        /// <summary>آخر يوم لصلاحية العرض (اختياري)</summary>
        public DateTime? ValidUntil { get; set; }
        public string Currency { get; set; } = "SYP";
        public string SupplyTerms { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public ProjectDocumentVersion? DocumentVersion { get; set; }

        public int RecordedByUserId { get; set; }
        public User RecordedByUser { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
        public ICollection<PricingOfferLine> Lines { get; set; } = new List<PricingOfferLine>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// سعر بند في عرض: سعر الوحدة والوحدة المعروضة، ونتيجة المطابقة الفنية، والاختيار (بسبب مكتوب). بند واحد مختار على الأكثر لكل بند طلب.
    /// الاختيار لا يتم آلياً: لا لأقل سعر ولا لما لم تتحقق مطابقته أو تختلف وحدته أو انتهت صلاحيته.
    /// </summary>
    public class PricingOfferLine
    {
        public int Id { get; set; }
        public int PricingOfferId { get; set; }
        public PricingOffer PricingOffer { get; set; } = null!;
        public int PricingRequestItemId { get; set; }
        public PricingRequestItem Item { get; set; } = null!;

        public decimal UnitPrice { get; set; }
        /// <summary>الوحدة التي سُعِّر بها (يجب أن تطابق وحدة البند لتُقارَن)</summary>
        public string Unit { get; set; } = string.Empty;
        public ConformityResult Conformity { get; set; } = ConformityResult.NotEvaluated;
        public string ConformityNote { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public string SelectionReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// محضر اتفاق مع متعهد جديد (النموذج المعتمد): الإعداد لمدير مكتب التسعير وحده بصلاحية مستقلة، والاعتماد بصلاحية أخرى لغير من أعدّه (حتى مدير النظام).
    /// المعتمد لا يُعدَّل. الرقم الداخلي AGR-{سنة}-{id} يُحسب، والرقم الرسمي <see cref="Number"/> يُدخَل.
    /// </summary>
    public class PricingAgreement
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public string? Number { get; set; }
        public DateTime AgreementDate { get; set; }
        /// <summary>المتعهد (من سجل مقاولي الفرع)</summary>
        public int SupplierId { get; set; }
        public Contractor Supplier { get; set; } = null!;
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }
        public int? ContractId { get; set; }
        public Contract? Contract { get; set; }
        public int? PricingRequestId { get; set; }
        public PricingRequest? PricingRequest { get; set; }

        public string TechnicalSpecs { get; set; } = string.Empty;
        public string ExecutionMethod { get; set; } = string.Empty;
        public string Currency { get; set; } = "SYP";
        public string Notes { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public ProjectDocumentVersion? DocumentVersion { get; set; }

        public AgreementStatus Status { get; set; } = AgreementStatus.Draft;
        public int PreparedByUserId { get; set; }
        public User PreparedByUser { get; set; } = null!;
        public DateTime? SubmittedAt { get; set; }
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovalNote { get; set; } = string.Empty;
        /// <summary>سبب إعادة المحضر للتصحيح</summary>
        public string ReturnNote { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<PricingAgreementLine> Lines { get; set; } = new List<PricingAgreementLine>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>سعر متفق عليه في محضر: بند بكميته ووحدته وسعر وحدته (بعملة المحضر)</summary>
    public class PricingAgreementLine
    {
        public int Id { get; set; }
        public int PricingAgreementId { get; set; }
        public PricingAgreement PricingAgreement { get; set; } = null!;
        public int SortOrder { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
    }
}
