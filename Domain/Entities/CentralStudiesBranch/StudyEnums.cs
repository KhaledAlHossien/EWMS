namespace Domain.Entities.CentralStudiesBranch
{
    // تعدادات فرع الدراسات المركزية — تُحفظ أرقاماً (INT). القيم ثابتة: لا يُعاد ترقيمها بعد النشر.

    /// <summary>حالة المستند الرسمي (الطلب، التكليف، الرد، أمر البدء)</summary>
    public enum StudyDocumentStatus
    {
        Draft = 1,            // مسودة: تُعدَّل وتُحذف
        PendingApproval = 2,  // أُرسل للاعتماد: ينتظر التواقيع بالترتيب
        Approved = 3,         // معتمد: يُبنى عليه المستند التالي
        Rejected = 4,         // رفضه أحد الموقّعين: يُعدَّل (يعود مسودة) ثم يُرسل من جديد
        Cancelled = 5         // ملغى: يبقى للأرشيف ولا يُحذف
    }

    public enum StudyUrgency { Urgent = 1, Normal = 2 }

    public enum StudyWorkType { Construction = 1, MaintenanceAndRestoration = 2 }

    public enum StudyPriority { TopSecretUrgent = 1, Urgent = 2, Normal = 3 }

    /// <summary>التوصية الفنية النهائية في إشعار الرد</summary>
    public enum StudyRecommendation { Accept = 1, Modify = 2, NotAvailable = 3 }

    public enum StudyBuildingType { Rehabilitation = 1, NewConstruction = 2 }

    public enum StudyProjectStatus { Running = 1, Suspended = 2, Completed = 3, Cancelled = 4 }

    public enum StudyStageStatus { NotStarted = 1, InProgress = 2, Closed = 3 }

    public enum StudyTaskStatus { NotStarted = 1, InProgress = 2, Done = 3 }

    /// <summary>مهمة المرحلة تُسند إلى قسم مشارك أو مكتب منه أو عضو في الفريق (نفس آلية لوحة المهام، بلا ارتباط بها)</summary>
    public enum StudyTaskTarget { Department = 1, Office = 2, User = 3 }

    /// <summary>عمود التمييز في جدول المخططات (TPH): كل نوع صنف مستقل في الكود</summary>
    public enum DrawingKind { Architectural = 1, Structural = 2, Mep = 3, Interior = 4, Technical = 5 }

    public enum DrawingStatus { InPreparation = 1, PendingReview = 2, Approved = 3, Rejected = 4 }

    /// <summary>قرار موقّع، وقرار اعتماد المخططات الأولي (CDA)</summary>
    public enum StudyDecision { Pending = 1, Approved = 2, ApprovedWithNotes = 3, Rejected = 4 }

    /// <summary>صفة الموقّع — لكل صفة صلاحية توقيع بحدّها الثابت (StudyRules.SignPermission)</summary>
    public enum StudySignerRole
    {
        FollowingDepartment = 1,      // القسم المتابع (قسم التكليف)
        PlanningAndProjects = 2,      // التخطيط وإدارة المشاريع
        CompetentDepartmentHead = 3,  // رئيس القسم المختص (قسم أمر البدء)
        BranchHead = 4,               // رئيس الفرع
        SendingDepartment = 5,        // القسم المرسل (قسم المخطط المعماري)
        DesignManagement = 6,         // إدارة التصميم
        QualityControl = 7,           // ضبط الجودة
        ProjectManagement = 8         // إدارة المشاريع
    }

    /// <summary>نوع المرحلة المرتبط بمستند: تُنشأ مغلقة تلقائياً مع المشروع وتواريخها من المستند</summary>
    public enum StudyLinkedDocument { None = 0, Request = 1, Assignment = 2, Response = 3, StartOrder = 4 }

    /// <summary>عدّاد الترقيم: رقم متسلسل لكل نوع وسنة</summary>
    public enum StudyDocumentType { Request = 1, Assignment = 2, StartOrder = 3, DesignApproval = 4, Response = 5 }

    /// <summary>نوع السجل المتأثر في سجل الدراسة، ومالك المرفق</summary>
    public enum StudyEntityType
    {
        Request = 1,
        Assignment = 2,
        Response = 3,
        StartOrder = 4,
        Project = 5,
        Stage = 6,
        Task = 7,
        Drawing = 8,
        Version = 9,
        DesignApproval = 10,
        Attachment = 11,
        ProjectDepartment = 12,
        ProjectMember = 13
    }

    public enum StudyLogAction
    {
        Created = 1,
        Edited = 2,
        Submitted = 3,   // أُرسل للاعتماد
        Approved = 4,    // توقيع بالموافقة أو اعتماد نهائي
        Rejected = 5,
        Cancelled = 6,
        Closed = 7,      // إغلاق مرحلة
        Deleted = 8,
        Reopened = 9     // إعادة فتح مرحلة
    }
}
