namespace Application.DTOs.Request
{
    /// <summary>قالب مهمة شخصي (إنشاء/تعديل)</summary>
    public class SaveTaskTemplateRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } = 2;
        /// <summary>مدة التسليم بالأيام من الإنشاء (null = بلا موعد)</summary>
        public int? DefaultDueDays { get; set; }
        public List<string> Items { get; set; } = [];
    }

    /// <summary>مهمة دورية: قالب + جهة + جدول</summary>
    public class SaveTaskRecurrenceRequestDto
    {
        public int TemplateId { get; set; }
        /// <summary>Department | Office | User</summary>
        public string TargetType { get; set; } = string.Empty;
        public int TargetId { get; set; }
        /// <summary>1 يومي، 2 أسبوعي، 3 شهري</summary>
        public int Frequency { get; set; }
        /// <summary>0 = الأحد … 6 = السبت (أسبوعي)</summary>
        public int? DayOfWeek { get; set; }
        /// <summary>1–31 (شهري؛ يُقصّ لآخر يوم في الشهر القصير)</summary>
        public int? DayOfMonth { get; set; }
        public int? DueAfterDays { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class SetRecurrenceActiveRequestDto
    {
        public bool IsActive { get; set; }
    }

    /// <summary>ربط بسجل: Reference رقم الطلب/الإجازة كما يظهر («MR-2026-00012» أو «12»)، أو EntityId مباشرة (موقع)</summary>
    public class AddTaskLinkRequestDto
    {
        /// <summary>MaintenanceRequest | Vacation | Site</summary>
        public string EntityType { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public int? EntityId { get; set; }
    }

    /// <summary>فلاتر تصدير المهام وإحصائياتها</summary>
    public class AssignedTaskExportFilterDto
    {
        public string Mode { get; set; } = "incoming";
        /// <summary>المنجزة المعروضة: آخر كم يوماً (الافتراضي 30)</summary>
        public int? DoneDays { get; set; }
        public string? Q { get; set; }
        public string? Priority { get; set; }
        public bool OverdueOnly { get; set; }
        public DateTime? DueFrom { get; set; }
        public DateTime? DueTo { get; set; }
    }
}
