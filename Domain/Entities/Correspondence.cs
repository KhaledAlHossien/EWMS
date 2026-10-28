namespace Domain.Entities
{
    /// <summary>اتجاه المعاملة. القيم مخزّنة — لا تُعاد استعمالها.</summary>
    public enum CorrespondenceDirection
    {
        Incoming = 1,   // وارد
        Outgoing = 2    // صادر
    }

    /// <summary>
    /// مرحلة الحالة (ثابتة، عليها يعتمد السلوك). الفرع يسمّي حالاته كما يشاء ويعيّن لكل حالة إحدى هذه المراحل
    /// (نمط مراحل حالات الصيانة). القيم مخزّنة — لا تُعاد استعمالها.
    /// </summary>
    public enum CorrespondenceStage
    {
        Registered = 1,   // مسجّلة (وصلت الديوان ولم تُعالَج)
        InProgress = 2,   // قيد المعالجة
        Answered = 3,     // تمّت الإجابة أو المعالجة (لا تُحتسب متأخرة)
        Closed = 4        // مغلقة: لا إحالة ولا تعديل إلا بإعادة فتحها
    }

    /// <summary>
    /// مراحل الصادر (مسار ثابت): مسودة ← معتمد بتوقيع (رئيس الفرع أو من يملك الاعتماد، وليس مُعدّها) ← صادر برقمه.
    /// الصادر لا يُحال، وبعد الصدور لا يُعدَّل.
    /// </summary>
    public enum OutgoingState
    {
        Draft = 1,
        Approved = 2,
        Issued = 3
    }

    /// <summary>نوع المعاملة (يضعه الفرع، مثل «أمر دراسة» أو «طلب استعلام»): مهلة المعالجة بالأيام اختيارية</summary>
    public class CorrespondenceType
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        /// <summary>مهلة المعالجة بالأيام التقويمية من التسجيل؛ null = بلا مهلة (لا تنبيه تأخر)</summary>
        public int? DeadlineDays { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    /// <summary>حالة معاملة بتسمية الفرع ومرحلة ثابتة تحدد سلوكها. اللون #RRGGBB للعرض.</summary>
    public class CorrespondenceStatus
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public CorrespondenceStage Stage { get; set; } = CorrespondenceStage.Registered;
        public string Color { get; set; } = "#8E8E93";
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// معاملة في ديوان الفرع (وارد أو صادر). تخص فرعاً واحداً، وتحملها وحدة (قسم/مكتب/موظف) تتغير بالإحالة.
    /// رقمها الداخلي IN-/OUT-{سنة}-{تسلسل} يُحسب من <see cref="Sequence"/> ولا يُخزَّن نصاً؛ وللمسودة بلا تسلسل حتى الصدور.
    /// لا تُحذف: تُغلق بحالة ذات مرحلة «مغلقة». ما فيها من إحالات وتدقيق لا يُعدَّل.
    /// </summary>
    public class Correspondence
    {
        public int Id { get; set; }

        public CorrespondenceDirection Direction { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        /// <summary>قسم من سجّلها (الديوان عادةً)</summary>
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        /// <summary>السنة والتسلسل داخل (الفرع، الاتجاه، السنة). تسلسل الصادر يُعطى عند الصدور فقط.</summary>
        public int Year { get; set; }
        public int? Sequence { get; set; }

        /// <summary>رقم الكتاب وتاريخه عند الجهة المرسلة (للوارد) أو في كتابنا الصادر</summary>
        public string? ExternalNumber { get; set; }
        public DateTime? ExternalDate { get; set; }
        /// <summary>الجهة المرسلة (وارد) أو المرسَل إليها (صادر)، نص حر</summary>
        public string Party { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;

        public int TypeId { get; set; }
        public CorrespondenceType Type { get; set; } = null!;

        /// <summary>حالة الوارد (لازمة له). الصادر يسير بـ<see cref="OutgoingState"/> فلا حالة له.</summary>
        public int? StatusId { get; set; }
        public CorrespondenceStatus? Status { get; set; }

        /// <summary>مشروع مرتبط (اختياري، ويمكن ربطه لاحقاً)</summary>
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        /// <summary>
        /// الجهة الحاملة الآن: يرى المعاملة ويعالجها حاملو HandleCorrespondence في هذه الوحدة (القاعدة 8: الإحالة لا تفتحها لكل القسم)،
        /// ومعها موظف بعينه اختيارياً. عند التسجيل تساوي وحدة المسجِّل.
        /// </summary>
        public int? CurrentDepartmentId { get; set; }
        public Department? CurrentDepartment { get; set; }
        public int? CurrentOfficeId { get; set; }
        public Office? CurrentOffice { get; set; }
        public int? CurrentUserId { get; set; }
        public User? CurrentUser { get; set; }

        /// <summary>موعد المعالجة = تاريخ التسجيل + مهلة النوع (وارد). لا يتغير بالإحالة.</summary>
        public DateTime? DueDate { get; set; }
        public DateTime? DueSoonNotifiedAt { get; set; }
        public DateTime? OverdueNotifiedAt { get; set; }

        // ===== الصادر: مسودة ← اعتماد ← صدور =====
        public OutgoingState? OutgoingState { get; set; }
        /// <summary>ردّ على وارد (اختياري): يربط الكتاب الصادر بالمعاملة الواردة التي يجيب عنها</summary>
        public int? ReplyToId { get; set; }
        public Correspondence? ReplyTo { get; set; }
        /// <summary>سبب إعادة المسودة من المعتمِد (يُمسح عند اعتمادها)</summary>
        public string? ReturnNote { get; set; }
        /// <summary>وقت رفع المسودة للاعتماد (UTC). null = لم تُرفع، أو أُعيدت، أو تغيّر محتواها بعد الرفع فتحتاج رفعاً جديداً</summary>
        public DateTime? SubmittedAt { get; set; }
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public DateTime? ApprovedAt { get; set; }
        /// <summary>إصدار توقيع المعتمِد لحظة الاعتماد (يثبت للطباعة اللاحقة؛ null = اعتُمد بلا توقيع مرفوع)</summary>
        public int? ApprovedSignatureId { get; set; }
        public UserSignature? ApprovedSignature { get; set; }
        public DateTime? IssuedAt { get; set; }
        public int? IssuedByUserId { get; set; }
        public User? IssuedByUser { get; set; }

        public int RegisteredByUserId { get; set; }
        public User RegisteredByUser { get; set; } = null!;
        public DateTime RegisteredAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? ClosedByUserId { get; set; }
        public User? ClosedByUser { get; set; }

        public ICollection<CorrespondenceReferral> Referrals { get; set; } = new List<CorrespondenceReferral>();
        public ICollection<CorrespondenceAttachment> Attachments { get; set; } = new List<CorrespondenceAttachment>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// إحالة معاملة إلى وحدة (ومعها موظف اختيارياً). سجل لا يُعدَّل ولا يُحذف؛ الاستلام يُسجَّل مرة واحدة.
    /// </summary>
    public class CorrespondenceReferral
    {
        public int Id { get; set; }
        public int CorrespondenceId { get; set; }
        public Correspondence Correspondence { get; set; } = null!;

        public int FromUserId { get; set; }
        public User FromUser { get; set; } = null!;
        public int? FromDepartmentId { get; set; }
        public Department? FromDepartment { get; set; }

        public int ToDepartmentId { get; set; }
        public Department ToDepartment { get; set; } = null!;
        public int? ToOfficeId { get; set; }
        public Office? ToOffice { get; set; }
        public int? ToUserId { get; set; }
        public User? ToUser { get; set; }

        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>أول من استلمها من الجهة المحالة إليها (يعالج أو يؤكد الاستلام)</summary>
        public DateTime? AcknowledgedAt { get; set; }
        public int? AcknowledgedByUserId { get; set; }
        public User? AcknowledgedByUser { get; set; }
    }

    /// <summary>مرفق معاملة (كتاب ممسوح أو ملحق أو مسودة الصادر): PDF أو صورة أو Word. يُضاف ولا يُستبدل، والمحتوى في مجلد المرفقات أو القاعدة</summary>
    public class CorrespondenceAttachment
    {
        public int Id { get; set; }
        public int CorrespondenceId { get; set; }
        public Correspondence Correspondence { get; set; } = null!;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;   // يُحدَّد من محتوى الملف لا من المتصفح
        public long Size { get; set; }
        public string? StorageKey { get; set; }
        public int UploadedByUserId { get; set; }
        public User UploadedByUser { get; set; } = null!;
        public DateTime UploadedAt { get; set; }
        public CorrespondenceAttachmentContent? Content { get; set; }
    }

    public class CorrespondenceAttachmentContent
    {
        public int AttachmentId { get; set; }
        public CorrespondenceAttachment Attachment { get; set; } = null!;
        public byte[] Data { get; set; } = [];
    }
}
