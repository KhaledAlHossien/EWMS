namespace Domain.Entities
{
    /// <summary>
    /// مرحلة المشروع (ثابتة، يعتمد عليها السلوك). الإلغاء والإغلاق بقرار لا بمرور الوقت: النظام لا ينقل مشروعاً تلقائياً.
    /// القيم أرقام مخزّنة — لا تُعاد استعمالها.
    /// </summary>
    public enum ProjectStage
    {
        Study = 1,         // دراسة
        Contracting = 2,   // تعاقد
        Execution = 3,     // تنفيذ
        Handover = 4,      // استلام
        Warranty = 5,      // ضمان
        Closed = 6,        // مغلق
        Cancelled = 7      // ملغى (بديل الحذف: يبقى سجل المشروع ووثائقه)
    }

    /// <summary>
    /// مشروع هندسي (العمود الفقري لوحدات الإضبارة، 2026-10-27). يخص فرعاً واحداً ويعالجه قسم، ويُربط بموقع من سجل المواقع المشترك.
    /// لا يُحذف أبداً: له وثائق وسجل تدقيق، فيُلغى بتغيير مرحلته. رقمه الداخلي PRJ-{سنة الإنشاء}-{id} يُحسب ولا يُخزَّن.
    /// </summary>
    public class Project
    {
        public int Id { get; set; }

        /// <summary>الرقم المرجعي الرسمي من المركزية (اختياري، فريد داخل الفرع)</summary>
        public string? ReferenceNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>الجهة المستفيدة أو المالك</summary>
        public string Beneficiary { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        /// <summary>القسم الذي يعالج المشروع حالياً (يتغير مع الإحالة بين الأقسام)</summary>
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        /// <summary>موقع المشروع من السجل المشترك (قد لا يُعرف عند بدء الدراسة)</summary>
        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public ProjectStage Stage { get; set; } = ProjectStage.Study;

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
        public ICollection<ProjectDocument> Documents { get; set; } = new List<ProjectDocument>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>مشارك في المشروع: يراه ضمن حدّ «مشاريعي»، ويمتد المشروع إلى قسمه عند ViewDepartmentProjects</summary>
    public class ProjectMember
    {
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>صفته في المشروع (مثل «مهندس الموقع» أو «مشرف القطاع»)، نص حر</summary>
        public string Title { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
    }

    /// <summary>نوع الوثيقة (قائمة ثابتة من إضبارة المشروع، لتربطها المراحل اللاحقة تلقائياً). القيم لا تُعاد استعمالها.</summary>
    public enum ProjectDocumentKind
    {
        StudyOrder = 1,            // أمر الدراسة
        Correspondence = 2,        // مراسلة (واردة أو صادرة)
        SurveyAndModels = 3,       // مساحة ونمذجة
        ArchitecturalDrawing = 4,  // مخطط معماري
        StructuralDrawing = 5,     // مخطط إنشائي
        ElectricalDrawing = 6,     // مخطط كهربائي
        MechanicalDrawing = 7,     // مخطط ميكانيكي
        Quantities = 8,            // ملف كميات وتدقيق
        Planning = 9,              // تخطيط
        ApprovalDecision = 10,     // قرار اعتماد
        Contract = 11,             // عقد أو ملحق
        StartOrder = 12,           // أمر مباشرة
        Schedule = 13,             // جدول زمني
        DailyWeeklyReport = 14,    // تقرير يومي أو أسبوعي
        QualityReport = 15,        // تقرير جودة
        ContractorEvaluation = 16, // تقييم مقاول
        Pricing = 17,              // تسعير وعروض أسعار
        Invoice = 18,              // فاتورة أو كشف
        HandoverMinutes = 19,      // محضر استلام أو تسليم
        Warranty = 20,             // وثيقة ضمان
        ClosureDecision = 21,      // قرار إغلاق
        Other = 99                 // أخرى
    }

    /// <summary>
    /// وثيقة منطقية في إضبارة المشروع، ولها إصدارات متتالية (ProjectDocumentVersion). رفع ملف جديد يضيف إصداراً ولا يستبدل شيئاً.
    /// آخر إصدار = أعلى VersionNumber، والمعتمد = أحدث إصدار معتمد.
    /// </summary>
    public class ProjectDocument
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public ProjectDocumentKind Kind { get; set; } = ProjectDocumentKind.Other;

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public ICollection<ProjectDocumentVersion> Versions { get; set; } = new List<ProjectDocumentVersion>();
    }

    /// <summary>
    /// إصدار واحد من وثيقة. الإصدار المعتمد لا يُعدَّل ولا يُحذف؛ من رفعه لا يعتمده.
    /// المحتوى في المجلد (StorageKey) أو في جدول المحتوى بوضع Database — مثل بقية المرفقات.
    /// </summary>
    public class ProjectDocumentVersion
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public ProjectDocument Document { get; set; } = null!;

        /// <summary>1، 2، 3… داخل الوثيقة (فريد معها)</summary>
        public int VersionNumber { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        /// <summary>بصمة SHA-256 للمحتوى (64 خانة سداسية صغيرة) لإثبات أن الملف لم يتغير</summary>
        public string Sha256 { get; set; } = string.Empty;
        /// <summary>مفتاح الملف في مجلد المرفقات؛ null = المحتوى في ProjectDocumentVersionContent</summary>
        public string? StorageKey { get; set; }

        /// <summary>مصدر الوثيقة (الجهة التي وردت منها)</summary>
        public string Source { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;

        public int UploadedByUserId { get; set; }
        public User UploadedByUser { get; set; } = null!;
        public DateTime UploadedAt { get; set; }

        public bool IsApproved { get; set; }
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public ProjectDocumentVersionContent? Content { get; set; }
    }

    /// <summary>بايتات الإصدار في وضع Database (جدول منفصل حتى لا تُحمَّل القوائم بمحتواها)</summary>
    public class ProjectDocumentVersionContent
    {
        public int VersionId { get; set; }
        public ProjectDocumentVersion Version { get; set; } = null!;
        public byte[] Data { get; set; } = [];
    }

    /// <summary>
    /// سجل التدقيق العام (القاعدة 6 من تحليل الجنوبية): من فعل ماذا ومتى، للإلحاق فقط — لا تعديل ولا حذف في أي مكان من الكود.
    /// يُكتب في معاملة العملية نفسها فلا يوجد اعتماد أو تحويل بلا أثر. السجلات القديمة (الأجهزة، الصيانة، المهام) تبقى في جداولها.
    /// الفرع لقطة بلا مفتاح أجنبي (تحديد حدّ القراءة)، والمستخدم Restrict (يُعطَّل ولا يُحذف).
    /// </summary>
    public class AuditLog
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>الفعل: created / updated / stage / member-added / uploaded / approved / downloaded …</summary>
        public string Action { get; set; } = string.Empty;
        /// <summary>نوع السجل: project / document / version</summary>
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }
        public int? BranchId { get; set; }

        /// <summary>عنوان مقروء بالعربية لحظة الفعل (يبقى بعد تغيّر الاسم)</summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>التغييرات سطراً لكل حقل: «المرحلة: دراسة ← تعاقد»</summary>
        public string Details { get; set; } = string.Empty;
    }
}
