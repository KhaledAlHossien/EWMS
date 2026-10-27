namespace Application.DTOs.Response
{
    public class StartOrderResponseDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime StartDate { get; set; }
        public int CompletionDays { get; set; }
        public int ExecutionDays { get; set; }
        public decimal? StudyCost { get; set; }
        public int? SiteEngineerUserId { get; set; }
        public string? SiteEngineerName { get; set; }
        public int? SectorSupervisorUserId { get; set; }
        public string? SectorSupervisorName { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>إصدار من الجدول في القائمة (بلا بنوده)</summary>
    public class ScheduleVersionResponseDto
    {
        public int Id { get; set; }
        public int VersionNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int TaskCount { get; set; }
        public decimal ActualProgress { get; set; }
    }

    /// <summary>صورة المشروع الزمنية: أمر المباشرة وإصدارات الجدول ومؤشرات المعتمد منها</summary>
    public class ScheduleOverviewResponseDto
    {
        public int ProjectId { get; set; }
        public string ProjectNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public bool ProjectFinished { get; set; }
        public StartOrderResponseDto? StartOrder { get; set; }
        public List<ScheduleVersionResponseDto> Versions { get; set; } = [];
        public int? ApprovedScheduleId { get; set; }
        public int? DraftScheduleId { get; set; }

        // مؤشرات الجدول المعتمد (فارغة بلا جدول معتمد)
        public decimal? ActualProgress { get; set; }
        /// <summary>المخطط حتى اليوم على التواريخ الحالية</summary>
        public decimal? PlannedProgress { get; set; }
        /// <summary>المخطط حتى اليوم على خط الأساس (التواريخ الأصلية)</summary>
        public decimal? BaselineProgress { get; set; }
        /// <summary>بنود تجاوزت نهايتها ولم تكتمل: «مرشَّحة للتأخر» وليست تأخيراً مسجَّلاً</summary>
        public int OverdueCandidates { get; set; }

        public bool CanEditStartOrder { get; set; }
        public bool CanEditSchedule { get; set; }
    }

    public class ScheduleTaskResponseDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime? OriginalStart { get; set; }
        public DateTime? OriginalEnd { get; set; }
        /// <summary>الأيام المضافة بتمديد معتمد (نهاية حالية − نهاية أصلية)</summary>
        public int ExtraDays { get; set; }
        public decimal Weight { get; set; }
        public bool IsCritical { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int Progress { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public string Note { get; set; } = string.Empty;
        public int? AssigneeUserId { get; set; }
        public string? AssigneeName { get; set; }
        public bool IsOverdueCandidate { get; set; }
        /// <summary>التواريخ الأصلية مثبّتة: لا تتغير تواريخه الحالية إلا بتمديد معتمد</summary>
        public bool DatesLocked { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class ScheduleDependencyResponseDto
    {
        public int Id { get; set; }
        public int PredecessorId { get; set; }
        public int SuccessorId { get; set; }
        /// <summary>اللاحق يبدأ في يوم نهاية سابقه أو قبله (تحذير لا منع)</summary>
        public bool HasConflict { get; set; }
    }

    /// <summary>إصدار كامل: بنوده وتبعياته ومؤشراته وما يستطيع المستخدم فعله عليه</summary>
    public class ScheduleDetailResponseDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public decimal ActualProgress { get; set; }
        public decimal PlannedProgress { get; set; }
        public decimal BaselineProgress { get; set; }
        public int OverdueCandidates { get; set; }
        /// <summary>تنبيهات لا تمنع شيئاً: لا أمر مباشرة، بند يسبق المباشرة، تعارض تبعيات…</summary>
        public List<string> Warnings { get; set; } = [];

        public List<ScheduleTaskResponseDto> Tasks { get; set; } = [];
        public List<ScheduleDependencyResponseDto> Dependencies { get; set; } = [];

        public bool CanEdit { get; set; }
        public bool CanApprove { get; set; }
        public bool CanUpdateProgress { get; set; }
        public bool CanClassify { get; set; }
    }

    public class ScheduleProgressEntryResponseDto
    {
        public long Id { get; set; }
        public int Progress { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
