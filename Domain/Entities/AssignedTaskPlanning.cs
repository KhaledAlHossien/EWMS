using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// قالب مهمة شخصي (قرار المستخدم 2026-10-08): عنوان ووصف وأولوية ومدة تسليم وبنود تحقق جاهزة يُنشأ منها مهام جديدة بنقرة.
    /// القالب خاص بصاحبه (لا يراه غيره)، واسمه فريد عنده.
    /// </summary>
    public class AssignedTaskTemplate
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public User OwnerUser { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AssignedTaskPriority Priority { get; set; } = AssignedTaskPriority.Normal;
        /// <summary>مدة التسليم بالأيام من تاريخ الإنشاء (null = بلا موعد)</summary>
        public int? DefaultDueDays { get; set; }

        public ICollection<AssignedTaskTemplateItem> Items { get; set; } = new List<AssignedTaskTemplateItem>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AssignedTaskTemplateItem
    {
        public int Id { get; set; }
        public int TemplateId { get; set; }
        public AssignedTaskTemplate Template { get; set; } = null!;
        public string Text { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// مهمة دورية: قالب + جهة + جدول (يومي/أسبوعي/شهري). يُنشئ العامل الخلفي مهمة جديدة في كل موعد بصلاحيات صاحبها وقتها،
    /// وإن تعذّر الإنشاء (فقد صلاحيته أو حُذفت الجهة…) تتوقف ويُبلَّغ صاحبها بالسبب. الفائت لا يُعوَّض (مهمة واحدة عند الاستئناف).
    /// </summary>
    public class AssignedTaskRecurrence
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public User OwnerUser { get; set; } = null!;
        public int TemplateId { get; set; }
        public AssignedTaskTemplate Template { get; set; } = null!;

        public AssignedTaskTargetType TargetType { get; set; }
        /// <summary>معرّف القسم أو المكتب أو الموظف حسب TargetType (بلا مفتاح أجنبي: حذف الجهة يوقف التكرار برسالة)</summary>
        public int TargetId { get; set; }

        public TaskRecurrenceFrequency Frequency { get; set; }
        public int? DayOfWeek { get; set; }     // 0 = الأحد … 6 = السبت (أسبوعي)
        public int? DayOfMonth { get; set; }    // 1–31، ويُقصّ لآخر يوم في الشهر (شهري)
        /// <summary>مدة التسليم من يوم الإنشاء (null = تُؤخذ من القالب)</summary>
        public int? DueAfterDays { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? NextRunDate { get; set; }
        public DateTime? LastRunAt { get; set; }
        public int? LastTaskId { get; set; }
        public string? LastError { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>ربط مهمة بسجل في نظام آخر (طلب صيانة / إجازة / موقع)؛ يرى المستخدمُ بيانات السجل فقط إن كان يحق له عرضه</summary>
    public class AssignedTaskLink
    {
        public int Id { get; set; }
        public int AssignedTaskId { get; set; }
        public AssignedTask AssignedTask { get; set; } = null!;
        public TaskLinkType EntityType { get; set; }
        public int EntityId { get; set; }
        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
