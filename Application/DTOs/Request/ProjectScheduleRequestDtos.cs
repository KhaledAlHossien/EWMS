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
}
