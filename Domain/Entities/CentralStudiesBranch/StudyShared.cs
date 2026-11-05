namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>
    /// سطر توقيع مطلوب على مستند، بترتيبه. مالك واحد فقط (قوس حصري، قيد CHECK): التكليف أو الرد أو أمر البدء أو CDA أو المشروع.
    /// يحفظ الموقّع ومسماه ونسخة توقيعه لحظة القرار، فإعادة الطباعة لا تتغير.
    /// </summary>
    public class StudyApproval
    {
        public int Id { get; set; }

        public int? AssignmentId { get; set; }
        public StudyAssignment? Assignment { get; set; }
        public int? ResponseId { get; set; }
        public StudyAssignmentResponse? Response { get; set; }
        public int? StartOrderId { get; set; }
        public StudyStartOrder? StartOrder { get; set; }
        public int? DesignApprovalId { get; set; }
        public DesignApproval? DesignApproval { get; set; }
        public int? ProjectId { get; set; }
        public StudyProject? Project { get; set; }

        public int? SignerUserId { get; set; }
        public User? Signer { get; set; }

        public int? SignatureId { get; set; }
        public UserSignature? Signature { get; set; }

        public StudySignerRole SignerRole { get; set; }
        public int SortOrder { get; set; }
        public StudyDecision Decision { get; set; }
        /// <summary>المسمى الوظيفي لحظة القرار (اسم دور الموقّع)</summary>
        public string? JobTitle { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string? Note { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// مرفق في فرع الدراسات — جدول واحد لكل المرفقات، مالك واحد فقط (قيد CHECK على الأعمدة السبعة).
    /// المحتوى في مجلد المرفقات على الخادم **دائماً** (StorageKey)، أيّاً كان FileStorage:Mode — ملفات المخططات قد تبلغ جيجابايت،
    /// فلا تُحفظ في القاعدة ولا سقف لحجمها (قرار المستخدم 2026-11-05).
    /// </summary>
    public class StudyAttachment
    {
        public int Id { get; set; }

        public int? RequestId { get; set; }
        public StudyRequest? Request { get; set; }
        public int? AssignmentId { get; set; }
        public StudyAssignment? Assignment { get; set; }
        public int? ResponseId { get; set; }
        public StudyAssignmentResponse? Response { get; set; }
        public int? StartOrderId { get; set; }
        public StudyStartOrder? StartOrder { get; set; }
        public int? ProjectId { get; set; }
        public StudyProject? Project { get; set; }
        public int? DrawingId { get; set; }
        public ProjectDrawing? Drawing { get; set; }
        public int? VersionId { get; set; }
        public ArchitecturalVersion? Version { get; set; }

        public int AttachmentTypeId { get; set; }
        public StudyAttachmentType AttachmentType { get; set; } = null!;

        public int UploadedByUserId { get; set; }
        public User UploadedBy { get; set; } = null!;

        public string FileName { get; set; } = string.Empty;
        /// <summary>يُكشف من محتوى الملف لا من امتداده</summary>
        public string ContentType { get; set; } = string.Empty;
        /// <summary>بالبايت (long: بلا سقف 2GB)</summary>
        public long Size { get; set; }
        /// <summary>مفتاح الملف في مجلد المرفقات (studies/yyyy/MM/…)</summary>
        public string StorageKey { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    /// <summary>
    /// سجل الدراسة: إلحاقي، لكل ما يحدث من الطلب حتى إغلاق المشروع، مجمّع على الطلب (جذر السلسلة).
    /// يحفظ أسباب الإلغاء والرفض ومن فعل ماذا ومتى.
    /// </summary>
    public class StudyActivityLog
    {
        public int Id { get; set; }

        public int RequestId { get; set; }
        public StudyRequest Request { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public StudyEntityType EntityType { get; set; }
        public int EntityId { get; set; }
        public StudyLogAction Action { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>عدّاد الترقيم: آخر رقم صدر لكل نوع مستند وسنة (يُقرأ ويُزاد داخل معاملة بقفل السطر)</summary>
    public class StudyNumberSequence
    {
        public StudyDocumentType DocumentType { get; set; }
        public int Year { get; set; }
        public int LastNumber { get; set; }
    }
}
