namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>
    /// نوع مرحلة (قائمة ديناميكية تديرها إدارة المشاريع). عند إنشاء مشروع تُنسخ الأنواع الفعّالة إلى مراحله
    /// بترتيبها ووزنها الافتراضيين؛ النوع المرتبط بمستند تُنشأ مرحلته مغلقة وتواريخها من ذلك المستند.
    /// </summary>
    public class StudyStageType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DefaultOrder { get; set; }
        public decimal DefaultWeight { get; set; }
        public StudyLinkedDocument LinkedDocument { get; set; }
        /// <summary>مسار المشاريع التي تُنسخ إليها: الدورة الكاملة (المركزية) أو التنسيق (الأفرع الجغرافية)</summary>
        public StudyTrack Track { get; set; }
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// فرع دراسة: المركزية (0) والأفرع الجغرافية (الشمالية 1، الجنوبية 2، الوسطى 3، الغربية 4، الشرقية 5).
    /// ليست فروعاً في النظام: كلها تتبع فرع الدراسات (BranchId) الذي يبقى حدّ الصلاحيات، والطلب يختار واحداً منها.
    /// Code يدخل في كود المشروع، وTrack يحدد مسار الدراسة. لا يتغير الرمز ولا المسار بعد استعماله في طلب.
    /// </summary>
    public class StudyBranch
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public int Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public StudyTrack Track { get; set; }
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>نوع مرفق (كتاب، مخطط، صورة موقع…)</summary>
    public class StudyAttachmentType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    /// <summary>الجهات المستفيدة الرئيسية التي تُختار في طلب تجهيز الدراسة</summary>
    public class StudyBeneficiary
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>مطلوب عند الإدارة والاختيار؛ null للبيانات القديمة التي تحتاج استكمال الرمز</summary>
        public string? Code { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>اختصاص هندسي: مدني، معماري، ديكور، كهرباء، ميكانيك…</summary>
    public class EngineeringDiscipline
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
