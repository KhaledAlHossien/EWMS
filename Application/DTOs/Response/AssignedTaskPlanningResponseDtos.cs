namespace Application.DTOs.Response
{
    public class TaskTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string PriorityAr { get; set; } = string.Empty;
        public int? DefaultDueDays { get; set; }
        public List<string> Items { get; set; } = [];
    }

    public class TaskRecurrenceDto
    {
        public int Id { get; set; }
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string TaskTitle { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetTypeAr { get; set; } = string.Empty;
        public int TargetId { get; set; }
        public string TargetName { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public string ScheduleAr { get; set; } = string.Empty;
        public int? DayOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public int? DueAfterDays { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime? NextRunDate { get; set; }
        public DateTime? LastRunAt { get; set; }
        public int? LastTaskId { get; set; }
        public string? LastError { get; set; }
    }

    /// <summary>رابط مهمة بسجل؛ إن لم يحق للمستخدم عرض السجل يأتي Available=false بلا بياناته</summary>
    public class TaskLinkDto
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityTypeAr { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool Available { get; set; }
        public bool CanRemove { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
    }

    public class TaskStatsGroupDto
    {
        public string TargetType { get; set; } = string.Empty;
        public string TargetTypeAr { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Done { get; set; }
        public int Open { get; set; }
        public int Overdue { get; set; }
        /// <summary>نسبة المنجزة في موعدها من المنجزة ذات الموعد (%)، أو null</summary>
        public double? OnTimeRate { get; set; }
        public double? AvgDays { get; set; }
        public int Returned { get; set; }
    }

    public class TaskStatsMonthDto
    {
        public string Month { get; set; } = string.Empty;   // yyyy-MM
        public int Created { get; set; }
        public int Done { get; set; }
    }

    public class TaskStatsDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Total { get; set; }
        public int Open { get; set; }
        public int InReview { get; set; }
        public int Done { get; set; }
        /// <summary>مفتوحة تجاوزت موعدها الآن</summary>
        public int Overdue { get; set; }
        public double? OnTimeRate { get; set; }
        public double? AvgDays { get; set; }
        /// <summary>مهام أعادها المُسنِد مرة أو أكثر من المراجعة</summary>
        public int ReturnedTasks { get; set; }
        public List<TaskStatsGroupDto> ByTarget { get; set; } = [];
        public List<TaskStatsMonthDto> ByMonth { get; set; } = [];
    }
}
