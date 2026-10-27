namespace Application.DTOs.Request
{
    /// <summary>أمر المباشرة (واحد لكل مشروع). RowVersion = ما رآه المستخدم عند التعديل.</summary>
    public class StartOrderRequestDto
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime StartDate { get; set; }
        public int CompletionDays { get; set; }
        public int ExecutionDays { get; set; }
        public decimal? StudyCost { get; set; }
        /// <summary>مهندس الموقع ومشرف القطاع: من مشاركي المشروع</summary>
        public int? SiteEngineerUserId { get; set; }
        public int? SectorSupervisorUserId { get; set; }
        public string? Notes { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>مسودة جدول جديدة. إن وُجد جدول معتمد فهي نسخة منه (إعادة إصدار) تحمل تواريخه الأصلية.</summary>
    public class ScheduleDraftRequestDto
    {
        public string? Title { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>بند في مسودة الجدول. التصنيف «حرج» ليس هنا: له صلاحية وطلب مستقل.</summary>
    public class ScheduleTaskRequestDto
    {
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        /// <summary>وزن البند في نسبة المشروع (يدوي، أكبر من صفر)</summary>
        public decimal Weight { get; set; } = 1;
        public string? Note { get; set; }
        public int? AssigneeUserId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ReorderScheduleTasksRequestDto
    {
        /// <summary>معرّفات كل بنود المسودة بالترتيب الجديد (يجب أن تكون البنود كلها بلا زيادة ولا نقص)</summary>
        public List<int> TaskIds { get; set; } = [];
    }

    public class ScheduleDependencyRequestDto
    {
        public int PredecessorId { get; set; }
        public int SuccessorId { get; set; }
    }

    /// <summary>تحديث حالة بند معتمد ونسبته (يُسجَّل في سجل الإنجاز)</summary>
    public class UpdateScheduleProgressRequestDto
    {
        public int Status { get; set; }
        public int Progress { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ClassifyScheduleTaskRequestDto
    {
        public bool IsCritical { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    // ───────── التأخير والتمديد ─────────

    /// <summary>تسجيل تأخير على بند معتمد. Kind: 1 غير مبرَّر، 2 مبرَّر (يُرفع تبريره بعدها بطلب مستقل)</summary>
    public class ScheduleDelayRequestDto
    {
        public int Kind { get; set; } = 1;
        public DateTime DiscoveredAt { get; set; }
        public string? Reason { get; set; }
        public string? Actions { get; set; }
        /// <summary>بنود لاحقة يختارها المسجِّل متأثرة بالتأخير (من الجدول نفسه)</summary>
        public List<int> AffectedTaskIds { get; set; } = [];
    }

    /// <summary>رفع تبرير التأخير للاعتماد (يرفعه من سجّله وحده): السبب والجهة التي وافقت وتاريخها ومستند الموافقة</summary>
    public class JustifyScheduleDelayRequestDto
    {
        public string? Reason { get; set; }
        public string? Actions { get; set; }
        public string? ApprovalAuthority { get; set; }
        public DateTime? ApprovalDate { get; set; }
        /// <summary>إصدار وثيقة من وثائق المشروع نفسه</summary>
        public int? ApprovalDocumentVersionId { get; set; }
        public bool AffectsCriticalWork { get; set; }
        public List<int> AffectedTaskIds { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>قرار على تبرير تأخير أو طلب تمديد. الرفض بسبب.</summary>
    public class ScheduleDecisionRequestDto
    {
        public bool Approve { get; set; }
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ExtensionLineRequestDto
    {
        public int TaskId { get; set; }
        public int Days { get; set; }
        /// <summary>بند لاحق تُزاح بدايته ونهايته معاً (يختاره صاحب الطلب صراحة بعد مراجعة المقترحات)</summary>
        public bool MovesStart { get; set; }
    }

    /// <summary>طلب تمديد على الجدول المعتمد. Mode: 1 جماعي (عدد أيام واحد)، 2 مستقل. DayCount: 1 تقويمية، 2 أيام عمل.</summary>
    public class ScheduleExtensionRequestDto
    {
        public int? DelayId { get; set; }
        public int Mode { get; set; } = 1;
        public int DayCount { get; set; } = 1;
        public string? Reason { get; set; }
        public List<ExtensionLineRequestDto> Lines { get; set; } = [];
        /// <summary>إصدارات وثائق داعمة من وثائق المشروع نفسه</summary>
        public List<int> DocumentVersionIds { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }
}
