namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>قرار الديوان المركزي في كتاب الرد — يقرؤه ويحدّده رئيس الفرع عند الإحالة (لا موظف الديوان)</summary>
    public enum StudyDiwanDecision { Approved = 1, Rejected = 2 }

    /// <summary>
    /// مرحلة معاملة الديوان (طلب الواجهة 2026-11-28): أين المعاملة ومن عليه الخطوة. Status يبقى كباقي المستندات
    /// (المرحلة 1 = مسودة، 2 = بانتظار الاعتماد، 4 = معتمدة، 9 = ملغاة، والباقي = مرفوضة) وStage يفصّله.
    /// </summary>
    public enum StudyDiwanStage
    {
        Registering = 1,        // لدى الديوان: الصادر والرد المركزي وصورة كتابه
        AtBranchHead = 2,       // بانتظار رئيس الفرع: يحيل بموافقة أو برفض، أو يعيد إلى الديوان
        ReturnedToDiwan = 3,    // أعادها رئيس الفرع إلى الديوان لتصحيح المعاملة أو مرفقاتها
        CentralApproved = 4,    // موافقة مركزية: يصدر مدير المشاريع أمر البدء (ما لم تكن التوصية «غير متاح»)
        CentralRejected = 5,    // رفض مركزي بانتظار مدير المشاريع: تعديل تخطيطي أو فني (أو إلغاء)
        PlanningRevision = 6,   // تعديل تخطيطي: يعدّل مدير المشاريع جزء التخطيط ويوقّعه وحده (يبقى توقيع القسم المتابع)
        TechnicalRevision = 7,  // تعديل فني: الرد عند القسم المتابع، ثم توقيعه وتوقيع التخطيط
        ReadyToResend = 8,      // اعتُمد الرد المعدّل: يسجّل الديوان صادراً جديداً على المعاملة نفسها
        Cancelled = 9
    }

    /// <summary>نوع التعديل الذي يختاره مدير المشاريع بعد الرفض المركزي</summary>
    public enum StudyDiwanRevision { Planning = 1, Technical = 2 }

    /// <summary>
    /// معاملة الديوان (DWN) — مستند من مستندات الدراسة بين إشعار الرد المعتمد وأمر البدء، واحدة لكل رد (ResponseId فريد).
    /// موظف الديوان يسجّل الصادر والرد المركزي (رقمه وتاريخه) ويرفق صورة الكتاب ويرسلها؛ رئيس الفرع يقرأ الكتاب ويحيلها إلى
    /// مدير المشاريع بموافقة (← أمر البدء) أو برفض (← تعديل تخطيطي أو فني للرد)، أو يعيدها إلى الديوان. بعد اعتماد الرد المعدّل
    /// يسجّل الديوان صادراً جديداً على المعاملة نفسها (جولة جديدة). كل إحالة تُحفظ جولةً في StudyDiwanRounds.
    /// </summary>
    public class StudyDiwanTransaction : IStudyDocument
    {
        public int Id { get; set; }

        public int ResponseId { get; set; }
        public StudyAssignmentResponse Response { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public string TransactionNumber { get; set; } = string.Empty;   // DWN-26-0001

        /// <summary>كتاب الصادر إلى الديوان المركزي (الجولة الحالية)</summary>
        public string DispatchNumber { get; set; } = string.Empty;
        public DateTime DispatchDate { get; set; }

        /// <summary>كتاب الرد المركزي (الجولة الحالية، يُكمَل قبل الإرسال إلى رئيس الفرع)</summary>
        public string? ReplyNumber { get; set; }
        public DateTime? ReplyDate { get; set; }
        /// <summary>قرار الجولة الحالية كما حدّده رئيس الفرع عند الإحالة؛ فارغ قبلها</summary>
        public StudyDiwanDecision? CentralDecision { get; set; }

        public string? Notes { get; set; }

        public StudyDiwanStage Stage { get; set; } = StudyDiwanStage.Registering;
        /// <summary>رقم الجولة الحالية (1 أول صادر؛ يزيد عند تسجيل صادر جديد بعد اعتماد الرد المعدّل)</summary>
        public int RoundNumber { get; set; } = 1;

        public StudyDocumentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public List<StudyDiwanRound> Rounds { get; set; } = [];
    }

    /// <summary>
    /// جولة من جولات المعاملة (لا تُعدَّل بعد إغلاقها): تُكتب عند إحالة رئيس الفرع وتحفظ صادرها وردها وقراره،
    /// ثم يُكمَل فيها اختيار مدير المشاريع (نوع التعديل وسببه) إن كان القرار رفضاً.
    /// </summary>
    public class StudyDiwanRound
    {
        public int Id { get; set; }

        public int TransactionId { get; set; }
        public StudyDiwanTransaction Transaction { get; set; } = null!;

        public int RoundNumber { get; set; }

        public string DispatchNumber { get; set; } = string.Empty;
        public DateTime DispatchDate { get; set; }
        public string ReplyNumber { get; set; } = string.Empty;
        public DateTime ReplyDate { get; set; }

        public StudyDiwanDecision CentralDecision { get; set; }
        public int ReferredByUserId { get; set; }
        public User ReferredBy { get; set; } = null!;
        public DateTime ReferredAt { get; set; }
        /// <summary>ملاحظة رئيس الفرع (سبب الرفض المركزي إلزامي)</summary>
        public string? ReferralNote { get; set; }

        public StudyDiwanRevision? RevisionKind { get; set; }
        public int? RevisionByUserId { get; set; }
        public User? RevisionBy { get; set; }
        public DateTime? RevisionAt { get; set; }
        public string? RevisionReason { get; set; }
    }
}
