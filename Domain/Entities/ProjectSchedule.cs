namespace Domain.Entities
{
    /// <summary>
    /// أمر المباشرة (المرحلة 1، من تحليل الجنوبية): أمر واحد لكل مشروع يصل من الديوان إلى إدارة المشاريع، ومنه يبدأ الجدول الزمني.
    /// القيم المالية التعاقدية (قيمة العقد والميزانية) تأتي مع العقود؛ هنا تكلفة الدراسة فقط. المرحلة لا تتغير بتسجيله تلقائياً.
    /// </summary>
    public class ProjectStartOrder
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        /// <summary>تاريخ المباشرة الفعلي (يبدأ منه الجدول)</summary>
        public DateTime StartDate { get; set; }
        /// <summary>مدة الإنجاز بالأيام</summary>
        public int CompletionDays { get; set; }
        /// <summary>زمن تنفيذ المشروع بالأيام (قد يختلف عن مدة الإنجاز)</summary>
        public int ExecutionDays { get; set; }
        public decimal? StudyCost { get; set; }

        /// <summary>مهندس الموقع ومشرف القطاع: من مشاركي المشروع</summary>
        public int? SiteEngineerUserId { get; set; }
        public User? SiteEngineer { get; set; }
        public int? SectorSupervisorUserId { get; set; }
        public User? SectorSupervisor { get; set; }

        public string Notes { get; set; } = string.Empty;

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public enum ScheduleStatus
    {
        Draft = 1,        // مسودة: تُحرَّر بنودها
        Approved = 2,     // معتمد: إصدار واحد فقط لكل مشروع، ولا تُعدَّل بنوده إلا بتمديد معتمد
        Superseded = 3    // حلّ محله إصدار أحدث (يبقى للمراجعة)
    }

    /// <summary>
    /// إصدار من الجدول الزمني للمشروع. مسودة ← معتمد ← سابق. من أنشأ الإصدار لا يعتمده. الإصدار الجديد ينسخ بنود سابقه
    /// ويحمل تواريخها الأصلية (خط الأساس لا يُمحى بإعادة الإصدار).
    /// </summary>
    public class ProjectSchedule
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public int VersionNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public ScheduleStatus Status { get; set; } = ScheduleStatus.Draft;

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public ICollection<ScheduleTask> Tasks { get; set; } = new List<ScheduleTask>();
        public byte[] RowVersion { get; set; } = [];
    }

    public enum ScheduleTaskStatus
    {
        NotStarted = 1,   // لم يبدأ
        InProgress = 2,   // قيد التنفيذ
        Completed = 3,    // مكتمل
        Suspended = 4     // موقوف
    }

    /// <summary>
    /// بند عمل في الجدول. PlannedStart/PlannedEnd = التواريخ الحالية (تُحرَّر في المسودة، وتتغير بعد الاعتماد بتمديد معتمد فقط).
    /// OriginalStart/OriginalEnd = تواريخ الاعتماد الأولى، تُثبَّت مرة ولا تتغير أبداً. الوزن يدخله المسؤول يدوياً (نسبة المشروع = متوسط مرجَّح).
    /// </summary>
    public class ScheduleTask
    {
        public int Id { get; set; }
        public int ScheduleId { get; set; }
        public ProjectSchedule Schedule { get; set; } = null!;

        public int SortOrder { get; set; }
        /// <summary>رمز اختياري (مثل 1.2)</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime? OriginalStart { get; set; }
        public DateTime? OriginalEnd { get; set; }

        /// <summary>وزن البند في نسبة المشروع (> 0، يدوي)</summary>
        public decimal Weight { get; set; } = 1;
        /// <summary>حرج أو غير حرج: يحدده مسؤول مخوّل (ClassifyScheduleTask)</summary>
        public bool IsCritical { get; set; }

        public ScheduleTaskStatus Status { get; set; } = ScheduleTaskStatus.NotStarted;
        /// <summary>نسبة الإنجاز 0–100</summary>
        public int Progress { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public string Note { get; set; } = string.Empty;
        /// <summary>نهاية البند التي نُبّه المخوّلون عن تجاوزها (مرة لكل نهاية؛ تمديد النهاية يسمح بتنبيه جديد)</summary>
        public DateTime? OverdueNotifiedFor { get; set; }

        public int? AssigneeUserId { get; set; }
        public User? Assignee { get; set; }

        public ICollection<ScheduleProgressEntry> ProgressHistory { get; set; } = new List<ScheduleProgressEntry>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>علاقة تبعية «نهاية ← بداية»: لا يبدأ اللاحق قبل نهاية السابق. الحلقات ممنوعة.</summary>
    public class ScheduleDependency
    {
        public int Id { get; set; }
        public int ScheduleId { get; set; }
        public ProjectSchedule Schedule { get; set; } = null!;
        public int PredecessorId { get; set; }
        public ScheduleTask Predecessor { get; set; } = null!;
        public int SuccessorId { get; set; }
        public ScheduleTask Successor { get; set; } = null!;
    }

    /// <summary>سجل تحديثات إنجاز البند (للإلحاق فقط): التاريخ والنسبة ومن حدّثها</summary>
    public class ScheduleProgressEntry
    {
        public long Id { get; set; }
        public int TaskId { get; set; }
        public ScheduleTask Task { get; set; } = null!;
        public int Progress { get; set; }
        public ScheduleTaskStatus Status { get; set; }
        public string Note { get; set; } = string.Empty;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    public enum DelayKind
    {
        Unjustified = 1,   // غير مبرَّر: يبقى في تاريخ المشروع ولا تُعدَّل تواريخ
        Justified = 2      // مبرَّر: يحتاج مستنداً وجهة موافقة واعتماداً
    }

    public enum DelayStatus
    {
        Recorded = 1,    // سُجّل (مبرَّر في مسودته، أو غير مبرَّر موثَّق)
        Submitted = 2,   // رُفع التبرير للاعتماد
        Approved = 3,    // اعتُمد التبرير
        Rejected = 4,    // رُفض
        Resolved = 5     // انتهى أثره (أُنجز البند أو مُدِّد)
    }

    /// <summary>
    /// تأخير مسجَّل على بند. مرور تاريخ البند وحده لا يُنشئه (مرشَّح للتأخر فقط): يسجّله مخوَّل بعد أن يراجع حالة العمل.
    /// التبرير لا يُقبل تلقائياً بمجرد رفع المرفق: يعتمده صاحب صلاحية مستقل عن من سجّله.
    /// </summary>
    public class ScheduleDelay
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public int TaskId { get; set; }
        public ScheduleTask Task { get; set; } = null!;

        public DelayKind Kind { get; set; } = DelayKind.Unjustified;
        public DelayStatus Status { get; set; } = DelayStatus.Recorded;
        /// <summary>تاريخ اكتشاف التأخير</summary>
        public DateTime DiscoveredAt { get; set; }
        /// <summary>نهاية البند المخططة لحظة تسجيل التأخير (تبقى كما كانت)</summary>
        public DateTime PlannedEndAtDiscovery { get; set; }

        public string Reason { get; set; } = string.Empty;
        /// <summary>الإجراءات المتخذة والملاحظات</summary>
        public string Actions { get; set; } = string.Empty;

        // مبرَّر: مستند الموافقة (وثيقة مشروع)، والجهة التي وافقت وتاريخ الموافقة، وأثره على الأعمال اللاحقة
        public int? ApprovalDocumentVersionId { get; set; }
        public ProjectDocumentVersion? ApprovalDocumentVersion { get; set; }
        public string ApprovalAuthority { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        /// <summary>الأعمال المتأثرة حرجة؟ (يحدده المخوّل)</summary>
        public bool AffectsCriticalWork { get; set; }

        public int RecordedByUserId { get; set; }
        public User RecordedByUser { get; set; } = null!;
        public DateTime RecordedAt { get; set; }

        public int? DecidedByUserId { get; set; }
        public User? DecidedByUser { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecisionNote { get; set; } = string.Empty;

        public ICollection<ScheduleDelayAffectedTask> AffectedTasks { get; set; } = new List<ScheduleDelayAffectedTask>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>بند متأثر بالتأخير (لاحق يحتاج إلى دراسة تمديد)</summary>
    public class ScheduleDelayAffectedTask
    {
        public int DelayId { get; set; }
        public ScheduleDelay Delay { get; set; } = null!;
        public int TaskId { get; set; }
        public ScheduleTask Task { get; set; } = null!;
    }

    public enum ExtensionMode
    {
        Group = 1,    // جماعي: بنود مختارة بعدد أيام واحد
        Single = 2    // مستقل: مدة لكل بند
    }

    /// <summary>طريقة عدّ أيام التمديد</summary>
    public enum ExtensionDayCount
    {
        Calendar = 1,       // أيام تقويمية
        WorkingDays = 2     // أيام عمل: بلا الجمعة والعطل الرسمية
    }

    public enum ExtensionStatus
    {
        Draft = 1,       // مسودة (تُعاين نتيجتها)
        Submitted = 2,   // رُفعت للاعتماد
        Approved = 3,    // اعتُمدت وطُبِّقت على التواريخ الحالية
        Rejected = 4
    }

    /// <summary>
    /// طلب تمديد على الجدول المعتمد. عند الاعتماد تتغير التواريخ الحالية للبنود دفعة واحدة والأصلية لا تتغير.
    /// صاحب الطلب لا يعتمده. الجدول لا يُمدَّد إلا بقرار معتمد.
    /// </summary>
    public class ScheduleExtension
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public int ScheduleId { get; set; }
        public ProjectSchedule Schedule { get; set; } = null!;
        public int? DelayId { get; set; }
        public ScheduleDelay? Delay { get; set; }

        public ExtensionMode Mode { get; set; } = ExtensionMode.Group;
        public ExtensionDayCount DayCount { get; set; } = ExtensionDayCount.Calendar;
        public ExtensionStatus Status { get; set; } = ExtensionStatus.Draft;
        public string Reason { get; set; } = string.Empty;

        public int RequestedByUserId { get; set; }
        public User RequestedByUser { get; set; } = null!;
        public DateTime RequestedAt { get; set; }

        public int? DecidedByUserId { get; set; }
        public User? DecidedByUser { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecisionNote { get; set; } = string.Empty;

        public ICollection<ScheduleExtensionItem> Items { get; set; } = new List<ScheduleExtensionItem>();
        public ICollection<ScheduleExtensionDocument> Documents { get; set; } = new List<ScheduleExtensionDocument>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// أثر التمديد على بند: التواريخ قبله وبعده. بند «ممدود» تتحرك نهايته فقط، وبند «مُزاح» (لاحق تأثر بسابقه)
    /// تتحرك بدايته ونهايته معاً بالعدد نفسه فتبقى مدته.
    /// </summary>
    public class ScheduleExtensionItem
    {
        public int Id { get; set; }
        public int ExtensionId { get; set; }
        public ScheduleExtension Extension { get; set; } = null!;
        public int TaskId { get; set; }
        public ScheduleTask Task { get; set; } = null!;

        public int Days { get; set; }
        public bool MovesStart { get; set; }
        public DateTime OldStart { get; set; }
        public DateTime OldEnd { get; set; }
        public DateTime NewStart { get; set; }
        public DateTime NewEnd { get; set; }
    }

    /// <summary>مستند داعم للتمديد (إصدار وثيقة من وثائق المشروع)</summary>
    public class ScheduleExtensionDocument
    {
        public int ExtensionId { get; set; }
        public ScheduleExtension Extension { get; set; } = null!;
        public int DocumentVersionId { get; set; }
        public ProjectDocumentVersion DocumentVersion { get; set; } = null!;
    }
}
