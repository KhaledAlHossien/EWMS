namespace Domain.Entities
{
    /// <summary>ترتيب المخرجات في أمر الدراسة: يقرره رئيس القسم لكل أمر. القيم لا تُعاد استعمالها.</summary>
    public enum StudyMode
    {
        Sequential = 1,   // بالتسلسل: لا يبدأ مخرج قبل اعتماد ما قبله في الترتيب
        Parallel = 2      // بالتوازي: كل المخرجات تُعدّ معاً
    }

    /// <summary>
    /// حالة أمر الدراسة (ثابتة). لا تنتقل بمرور الوقت: الإرسال للمركزية وقرارها يسجّلهما مخوَّل يدوياً.
    /// القيم لا تُعاد استعمالها.
    /// </summary>
    public enum StudyStatus
    {
        Open = 1,               // قيد الإعداد: مخرجاته تُعدّ وتُراجَع
        SentToCenter = 2,       // أُرسلت إلى المركزية وبانتظار قرارها
        ApprovedByCenter = 3,   // اعتمدتها المركزية
        ReturnedByCenter = 4,   // أعادتها المركزية بملاحظات (تُفتح جولة جديدة)
        Cancelled = 5           // ملغاة (بديل الحذف: يبقى أثرها)
    }

    /// <summary>مسار المخرج الفني: إعداد ← مراجعة ← معتمد أو معاد. القيم لا تُعاد استعمالها.</summary>
    public enum DeliverableState
    {
        Preparing = 1,   // قيد الإعداد
        InReview = 2,    // رُفع للمراجعة (مجمَّد حتى يبتّ فيه المراجع)
        Approved = 3,    // معتمد: إصداره المعتمد مثبَّت
        Returned = 4     // أعاده المراجع بسبب (يعود مُعدّه إلى الإعداد)
    }

    /// <summary>نتيجة إرسال الدراسة إلى المركزية (تُسجَّل يدوياً: لا ربط إلكتروني معها)</summary>
    public enum CenterDecision
    {
        Pending = 1,   // بانتظار القرار
        Approved = 2,  // اعتمدتها
        Notes = 3      // أعادتها بملاحظات
    }

    /// <summary>
    /// نوع مخرج فني يعرّفه الفرع بأسمائه (معماري، إنشائي، كهربائي، ميكانيكي، كميات…): بيانات لا كود. الاسم فريد داخل الفرع.
    /// نوع الوثيقة (<see cref="DocumentKind"/>) يحدد كيف يُصنَّف ملف المخرج في إضبارة المشروع.
    /// </summary>
    public class DeliverableType
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public ProjectDocumentKind DocumentKind { get; set; } = ProjectDocumentKind.Other;
        public int SortOrder { get; set; }
        /// <summary>المعطَّل يختفي من نماذج الإضافة وتبقى مخرجاته القديمة</summary>
        public bool IsActive { get; set; } = true;
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// أمر دراسة على مشروع (2026-10-28، المرحلة 3): يصل عادةً من الديوان (معاملة واردة) ويفتحه رئيس القسم، ثم تتفرع عنه
    /// مخرجات المكاتب الفنية. حدود رؤيته هي حدود المشروع نفسه. لا يُحذف: يُلغى. الرقم الداخلي STD-{سنة}-{id} يُحسب.
    /// </summary>
    public class StudyOrder
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        /// <summary>المعاملة الواردة التي وصل بها الأمر (اختياري)</summary>
        public int? CorrespondenceId { get; set; }
        public Correspondence? Correspondence { get; set; }

        /// <summary>رقم الأمر الرسمي كما ورد من المركزية (اختياري)</summary>
        public string? ExternalNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public StudyMode Mode { get; set; } = StudyMode.Sequential;
        public StudyStatus Status { get; set; } = StudyStatus.Open;
        /// <summary>موعد إنجاز الدراسة (اختياري؛ تجاوزه تنبيه لا قرار)</summary>
        public DateTime? DueDate { get; set; }

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<StudyDeliverable> Deliverables { get; set; } = new List<StudyDeliverable>();
        public ICollection<StudySubmission> Submissions { get; set; } = new List<StudySubmission>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// مخرج فني من أمر الدراسة يُسند إلى مكتب ومهندس من مشاركي المشروع. ملفه وثيقة في إضبارة المشروع (إصدارات متتالية):
    /// عند الرفع للمراجعة يُثبَّت الإصدار المرفوع، وعند الاعتماد يصير هو الإصدار المعتمد ولا يُستبدل.
    /// من رفع للمراجعة (أو رفع الملف) لا يراجع. نوع واحد لكل أمر.
    /// </summary>
    public class StudyDeliverable
    {
        public int Id { get; set; }
        public int StudyOrderId { get; set; }
        public StudyOrder StudyOrder { get; set; } = null!;

        public int TypeId { get; set; }
        public DeliverableType Type { get; set; } = null!;

        /// <summary>موضعه في التسلسل (يُطبَّق فقط عندما يكون الأمر بالتسلسل)</summary>
        public int SortOrder { get; set; }
        public int? OfficeId { get; set; }
        public Office? Office { get; set; }
        /// <summary>المهندس المُعِدّ: من مشاركي المشروع، وفي المكتب المسند إليه إن حُدّد</summary>
        public int? AssigneeUserId { get; set; }
        public User? Assignee { get; set; }
        public DateTime? DueDate { get; set; }
        public string Note { get; set; } = string.Empty;

        public DeliverableState State { get; set; } = DeliverableState.Preparing;

        /// <summary>وثيقة المخرج في الإضبارة (تُنشأ عند أول رفع)</summary>
        public int? DocumentId { get; set; }
        public ProjectDocument? Document { get; set; }

        /// <summary>الإصدار الذي رُفع للمراجعة (يُثبَّت لحظة الرفع)</summary>
        public int? SubmittedVersionId { get; set; }
        public ProjectDocumentVersion? SubmittedVersion { get; set; }
        public int? SubmittedByUserId { get; set; }
        public User? SubmittedByUser { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public int? ReviewedByUserId { get; set; }
        public User? ReviewedByUser { get; set; }
        public DateTime? ReviewedAt { get; set; }
        /// <summary>سبب الإعادة من المراجع (يُمسح عند الاعتماد)</summary>
        public string? ReturnNote { get; set; }
        /// <summary>الإصدار المعتمد (= المرفوع وقت الاعتماد؛ لا يتغير بعده)</summary>
        public int? ApprovedVersionId { get; set; }
        public ProjectDocumentVersion? ApprovedVersion { get; set; }

        /// <summary>موعد المخرج الذي نُبّه عن تجاوزه (مرة لكل موعد؛ تغيير الموعد يسمح بتنبيه جديد)</summary>
        public DateTime? OverdueNotifiedFor { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// جولة إرسال للمركزية: تُسجَّل يدوياً بتاريخها وقرار المركزية عليها (اعتماد أو ملاحظات). جولة واحدة بانتظار القرار كحد أقصى.
    /// الكتاب الصادر المرتبط اختياري (المعاملة الصادرة من الديوان).
    /// </summary>
    public class StudySubmission
    {
        public int Id { get; set; }
        public int StudyOrderId { get; set; }
        public StudyOrder StudyOrder { get; set; } = null!;

        /// <summary>1، 2، 3… داخل الأمر (فريد معه)</summary>
        public int Round { get; set; }
        public DateTime SentAt { get; set; }
        public int SentByUserId { get; set; }
        public User SentByUser { get; set; } = null!;
        public int? OutgoingCorrespondenceId { get; set; }
        public Correspondence? OutgoingCorrespondence { get; set; }
        public string Note { get; set; } = string.Empty;

        public CenterDecision Decision { get; set; } = CenterDecision.Pending;
        public DateTime? DecidedAt { get; set; }
        public int? DecidedByUserId { get; set; }
        public User? DecidedByUser { get; set; }
        /// <summary>ملاحظات المركزية (مطلوبة عند «ملاحظات»)</summary>
        public string DecisionNote { get; set; } = string.Empty;
        /// <summary>وثيقة قرار المركزية في الإضبارة (إصدار منها، اختياري)</summary>
        public int? DecisionVersionId { get; set; }
        public ProjectDocumentVersion? DecisionVersion { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
