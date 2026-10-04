namespace Application.DTOs.Response
{
    public class DeviceTypeResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class DeviceCompanyResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class DamageTypeResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class MaintenanceRequestStatusResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public bool IsDelivery { get; set; }
    }

    public class MaintenanceRequestResponseDto
    {
        public int Id { get; set; }

        /// <summary>الرقم المعروض: MR-2026-00125</summary>
        public string Number { get; set; } = string.Empty;

        // ما يستطيعه المستخدم الحالي على هذا السجل (النطاق فقط — الصلاحية تُفحص بالـ Policy)
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanAssign { get; set; }
        public bool CanChangeStatus { get; set; }

        public int UserId { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;

        public int DeviceTypeId { get; set; }
        public string DeviceTypeName { get; set; } = string.Empty;
        public int DamageTypeId { get; set; }
        public string DamageTypeName { get; set; } = string.Empty;
        public int DeviceCompanyId { get; set; }
        public string DeviceCompanyName { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;
        public string Accessories { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int MaintenanceRequestStatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class MaintenanceTaskResponseDto
    {
        public int Id { get; set; }

        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanAssign { get; set; }

        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        public string TaskLocation { get; set; } = string.Empty;
        public string RequestingParty { get; set; } = string.Empty;
        public string RequiredWork { get; set; } = string.Empty;
        public string CompletedWorks { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class MaintenanceActivityDto
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// بيانات الطباعة: إيصال الاستلام وورقة التسليم. بعد التسليم (Delivered) يُطبع الموقّع وتوقيعه كما ثُبِّتا لحظة التسليم؛
    /// قبله يُعرض اسم الموقّع المتوقع بلا توقيع.
    /// </summary>
    public class MaintenancePrintDto
    {
        public MaintenanceRequestResponseDto Request { get; set; } = new();
        public string ManagerName { get; set; } = string.Empty;
        public string? ManagerSignature { get; set; }
        public bool Delivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    // ==================== الإحصائيات ====================
    public class MaintenanceCountDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public int Count { get; set; }
    }

    public class MaintenanceMonthDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Count { get; set; }
    }

    public class MaintenanceStatsDto
    {
        public int TotalRequests { get; set; }
        public int RequestsThisMonth { get; set; }
        public int TotalTasks { get; set; }
        public int TasksThisMonth { get; set; }

        /// <summary>متوسط المدة بين تاريخ البدء وتاريخ الإنجاز بالساعات (للطلبات التي لها التاريخان)</summary>
        public double? AverageRepairHours { get; set; }

        public List<MaintenanceCountDto> ByStatus { get; set; } = [];
        public List<MaintenanceCountDto> ByTechnician { get; set; } = [];
        public List<MaintenanceCountDto> ByDamageType { get; set; } = [];
        public List<MaintenanceCountDto> ByDeviceType { get; set; } = [];
        public List<MaintenanceCountDto> ByCompany { get; set; } = [];
        public List<MaintenanceCountDto> TasksByUser { get; set; } = [];

        /// <summary>آخر 6 أشهر (الأقدم أولاً)</summary>
        public List<MaintenanceMonthDto> Monthly { get; set; } = [];
    }

    /// <summary>خيار في قائمة الفنيين (لفلتر البحث بالفني)</summary>
    public class TechnicianOptionDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}
