namespace Domain.Entities
{
    /// <summary>طبيعة التعاقد: تختلف الحقول المطلوبة بحسبها. القيم لا تُعاد استعمالها.</summary>
    public enum ContractingMethod
    {
        Direct = 1,       // تنفيذ مباشر
        Tender = 2,       // مناقصة
        PriceQuotes = 3   // عروض أسعار
    }

    /// <summary>حالة العقد: يغيّرها مخوَّل يدوياً (لا تنتقل بمرور الوقت). القيم لا تُعاد استعمالها.</summary>
    public enum ContractStatus
    {
        Contracting = 1,   // قيد التعاقد: وصل أمر التنفيذ وتجري الإجراءات
        Active = 2,        // ساري
        Completed = 3,     // منجز
        Terminated = 4,    // مفسوخ
        Cancelled = 5      // ملغى قبل سريانه
    }

    /// <summary>نوع اللجنة: الفنية والمالية للعقد، أو لجنة استلام لمحضر استلام</summary>
    public enum CommitteeKind
    {
        TechnicalFinancial = 1,
        Handover = 2
    }

    /// <summary>قرار الجهة المخولة في مبلغ محتجز: إفراج (نهائي) أو استمرار احتجاز</summary>
    public enum RetentionDecisionKind
    {
        Release = 1,
        Continue = 2
    }

    /// <summary>مسار تقييم المقاول: مسودة مهندس الموقع ← مرسل إلى مكتب العقود ← مراجَع ومحفوظ</summary>
    public enum EvaluationStatus
    {
        Draft = 1,
        Submitted = 2,
        Reviewed = 3
    }

    /// <summary>
    /// مقاول أو متعهد في سجل الفرع (بيانات لا كود). الاسم فريد داخل الفرع. لا يُحذف إن ارتبط بعقد أو عرض أو تقييم: يُعطَّل.
    /// </summary>
    public class Contractor
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        /// <summary>رقم السجل أو الترخيص الرسمي (اختياري)</summary>
        public string? RegistrationNumber { get; set; }
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string Notes { get; set; } = string.Empty;
        /// <summary>المعطَّل يختفي من قوائم الاختيار وتبقى عقوده وتقييماته</summary>
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// عقد على مشروع (2026-10-31، المرحلة 4): يفتحه مكتب العقود عند وصول أمر التنفيذ، وتتبعه طبيعة التعاقد وعروضه ولجنته وملاحقه
    /// وضمانه ومبالغه المحتجزة. المبالغ والمدد تُدخَل يدوياً كما اعتُمدت: لا معادلات (النسب والقواعد لم تُحدَّد بعد).
    /// حدّ رؤيته هو حدّ المشروع نفسه. لا يُحذف: يُلغى أو يُفسخ. الرقم الداخلي CON-{سنة}-{id} يُحسب، والرقم الرسمي <see cref="Number"/> يُدخَل.
    /// </summary>
    public class Contract
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        /// <summary>فرع المشروع (نسخة للفهرسة وفرادة الرقم الرسمي داخل الفرع)</summary>
        public int BranchId { get; set; }

        /// <summary>معاملة الديوان التي وصل بها أمر التنفيذ (اختياري)</summary>
        public int? CorrespondenceId { get; set; }
        public Correspondence? Correspondence { get; set; }
        public string? ExecutionOrderNumber { get; set; }
        public DateTime? ExecutionOrderDate { get; set; }

        public ContractingMethod Method { get; set; } = ContractingMethod.Direct;
        public ContractStatus Status { get; set; } = ContractStatus.Contracting;

        /// <summary>المتعاقد معه: يُحدَّد باختيار عرض أو إرساء مناقصة أو مباشرة، وإلا فارغ أثناء التعاقد</summary>
        public int? ContractorId { get; set; }
        public Contractor? Contractor { get; set; }

        /// <summary>الرقم الرسمي للعقد (يُدخَل عند إبرامه؛ فريد داخل الفرع)</summary>
        public string? Number { get; set; }
        public DateTime? ContractDate { get; set; }
        public DateTime? StartDate { get; set; }
        public int? DurationDays { get; set; }
        /// <summary>القيمة التعاقدية كما اعتُمدت (غير تكلفة الدراسة ولا الميزانية المعتمدة)</summary>
        public decimal? Value { get; set; }
        /// <summary>رمز العملة (SYP مثلاً)</summary>
        public string Currency { get; set; } = "SYP";
        public string Scope { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        // المناقصة (تُملأ عندما تكون الطبيعة مناقصة)
        public string? TenderNumber { get; set; }
        public DateTime? TenderSentAt { get; set; }
        public DateTime? TenderOpenedAt { get; set; }
        public DateTime? AwardedAt { get; set; }
        public string AwardNote { get; set; } = string.Empty;

        // الضمان: المدة المعتمدة وتاريخا بدايته ونهايته يحددهما مكتب العقود بحسب القواعد الرسمية
        public int? WarrantyMonths { get; set; }
        public DateTime? WarrantyStart { get; set; }
        public DateTime? WarrantyEnd { get; set; }
        /// <summary>نهاية الضمان التي نُبّه عن قربها (مرة لكل نهاية؛ تغييرها يسمح بتنبيه جديد)</summary>
        public DateTime? WarrantyNotifiedFor { get; set; }

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<ContractOffer> Offers { get; set; } = new List<ContractOffer>();
        public ICollection<ContractAmendment> Amendments { get; set; } = new List<ContractAmendment>();
        public ICollection<Committee> Committees { get; set; } = new List<Committee>();
        public ICollection<RetentionEntry> RetentionEntries { get; set; } = new List<RetentionEntry>();
        public ICollection<ContractorEvaluation> Evaluations { get; set; } = new List<ContractorEvaluation>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>عرض مقدَّم على عقد (مناقصة أو عروض أسعار). عرض واحد لكل متعهد، وواحد على الأكثر مُرسًّى عليه. سجل لا يُعدَّل بعد الإرساء.</summary>
    public class ContractOffer
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public Contract Contract { get; set; } = null!;
        public int ContractorId { get; set; }
        public Contractor Contractor { get; set; } = null!;

        public decimal Amount { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string Note { get; set; } = string.Empty;
        /// <summary>العرض الذي رست عليه المناقصة أو اختير بالقرار الرسمي</summary>
        public bool IsAwarded { get; set; }

        public int RecordedByUserId { get; set; }
        public User RecordedByUser { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
    }

    /// <summary>ملحق معتمد على عقد: سجل يُضاف ولا يُعدَّل (ADR-0007). وثيقته إصدار مثبَّت من إضبارة المشروع (اختياري).</summary>
    public class ContractAmendment
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public Contract Contract { get; set; } = null!;

        /// <summary>1، 2، 3… داخل العقد (فريد معه)</summary>
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>أثره على القيمة (موجب أو سالب) إن وُجد</summary>
        public decimal? ValueChange { get; set; }
        /// <summary>أثره على مدة التنفيذ بالأيام (موجب أو سالب) إن وُجد</summary>
        public int? DurationChangeDays { get; set; }
        public DateTime ApprovedAt { get; set; }
        public int? DocumentVersionId { get; set; }
        public ProjectDocumentVersion? DocumentVersion { get; set; }

        public int RecordedByUserId { get; set; }
        public User RecordedByUser { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
    }

    /// <summary>
    /// لجنة بقرار تشكيل: فنية ومالية لعقد (يشكّلها رئيس قسم الإشراف)، أو لجنة استلام لمحضر استلام. أعضاؤها موظفون أو أسماء من خارج النظام،
    /// ومحاضرها وثائق في إضبارة المشروع. تخص عقداً أو محضر استلام واحداً (قيد في قاعدة البيانات).
    /// </summary>
    public class Committee
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public CommitteeKind Kind { get; set; }
        public int? ContractId { get; set; }
        public Contract? Contract { get; set; }
        public int? HandoverRecordId { get; set; }
        public HandoverRecord? HandoverRecord { get; set; }

        public DateTime FormedAt { get; set; }
        public string? DecisionNumber { get; set; }
        /// <summary>وثيقة قرار التشكيل (إصدار مثبَّت، اختياري)</summary>
        public int? DecisionVersionId { get; set; }
        public ProjectDocumentVersion? DecisionVersion { get; set; }
        /// <summary>التوصيات والقرارات المعتمدة للجنة</summary>
        public string Recommendations { get; set; } = string.Empty;

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public ICollection<CommitteeMember> Members { get; set; } = new List<CommitteeMember>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>عضو في لجنة: موظف في النظام أو اسم من خارجه (واحد منهما)، بصفته، ورئيس واحد على الأكثر</summary>
    public class CommitteeMember
    {
        public int Id { get; set; }
        public int CommitteeId { get; set; }
        public Committee Committee { get; set; } = null!;
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string? ExternalName { get; set; }
        /// <summary>الصفة: مسؤول العقود، ممثل مكتب التسعير، ممثل المساحة والكميات…</summary>
        public string Capacity { get; set; } = string.Empty;
        public bool IsChair { get; set; }
    }

    /// <summary>مبلغ محتجز على عقد خلال الضمان (يُدخَل يدوياً). سجل لا يُعدَّل؛ قرار الإفراج أو الاستمرار سجل منفصل.</summary>
    public class RetentionEntry
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public Contract Contract { get; set; } = null!;
        public decimal Amount { get; set; }
        public DateTime WithheldAt { get; set; }
        public string Note { get; set; } = string.Empty;
        public int RecordedByUserId { get; set; }
        public User RecordedByUser { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
        public ICollection<RetentionDecision> Decisions { get; set; } = new List<RetentionDecision>();
    }

    /// <summary>قرار في مبلغ محتجز بالاعتماد الرسمي: إفراج أو استمرار احتجاز، بجهة الاعتماد ووثيقة مؤيدة اختيارية. الإفراج نهائي.</summary>
    public class RetentionDecision
    {
        public int Id { get; set; }
        public int RetentionEntryId { get; set; }
        public RetentionEntry RetentionEntry { get; set; } = null!;
        public RetentionDecisionKind Decision { get; set; }
        public DateTime DecidedAt { get; set; }
        /// <summary>الجهة التي اعتمدت القرار (كما في الوثيقة الرسمية)</summary>
        public string ApprovingAuthority { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int? DocumentVersionId { get; set; }
        public ProjectDocumentVersion? DocumentVersion { get; set; }
        public int RecordedByUserId { get; set; }
        public User RecordedByUser { get; set; } = null!;
        public DateTime RecordedAt { get; set; }
    }

    /// <summary>
    /// معيار تقييم يعرّفه الفرع بأسمائه (بيانات لا كود). الوزن اختياري، ولا يُحتسب أي تصنيف تلقائي (المعايير وطريقة الدرجة لم تُعتمد رسمياً).
    /// الاسم فريد داخل الفرع.
    /// </summary>
    public class EvaluationCriterion
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Weight { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// تقييم مقاول على عقد يعدّه مهندس الموقع ويرسله إلى مكتب العقود فيراجعه غيره ويحفظه في ملف المقاول. الدرجات تُدخَل يدوياً؛ اسم المعيار ووزنه
    /// يُنسخان لحظة الإضافة فلا يغيّرهما تعديل المعيار لاحقاً. المراجَع لا يُعدَّل.
    /// </summary>
    public class ContractorEvaluation
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public Contract Contract { get; set; } = null!;
        /// <summary>المقاول وقت التقييم</summary>
        public int ContractorId { get; set; }
        public Contractor Contractor { get; set; } = null!;

        public int EvaluatedByUserId { get; set; }
        public User EvaluatedByUser { get; set; } = null!;
        public DateTime EvaluationDate { get; set; }
        public EvaluationStatus Status { get; set; } = EvaluationStatus.Draft;
        public string Notes { get; set; } = string.Empty;
        /// <summary>وثيقة داعمة (إصدار مثبَّت، اختياري)</summary>
        public int? DocumentVersionId { get; set; }
        public ProjectDocumentVersion? DocumentVersion { get; set; }

        public DateTime? SubmittedAt { get; set; }
        public int? ReviewedByUserId { get; set; }
        public User? ReviewedByUser { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string ReviewNote { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<EvaluationScore> Scores { get; set; } = new List<EvaluationScore>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>درجة معيار واحد في تقييم (0–100 يدوية، اختيارية)، مع نسخة من اسم المعيار ووزنه وقت التقييم</summary>
    public class EvaluationScore
    {
        public int Id { get; set; }
        public int EvaluationId { get; set; }
        public ContractorEvaluation Evaluation { get; set; } = null!;
        public int CriterionId { get; set; }
        public EvaluationCriterion Criterion { get; set; } = null!;
        public string CriterionName { get; set; } = string.Empty;
        public decimal? Weight { get; set; }
        public decimal? Score { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}
