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
        public int Stage { get; set; }
    }

    public class DeviceMaintenanceResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DeviceTypeId { get; set; }
        public string DeviceTypeName { get; set; } = string.Empty;
        public int DeviceCompanyId { get; set; }
        public string DeviceCompanyName { get; set; } = string.Empty;
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
        public bool CanRequestTransfer { get; set; }

        public int UserId { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        public int? ClientUserId { get; set; }
        /// <summary>قسم العميل الموظف حالياً (فارغ للعميل الخارجي)</summary>
        public string ClientDepartmentName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;

        // بيانات الجهاز (من DeviceMaintenance) — مسطّحة هنا حتى تعرضها القوائم والطباعة دون طلب إضافي
        public int DeviceMaintenanceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
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
        /// <summary>مرحلة الحالة الحالية، والطلب مُغلق في «مُسلَّم» و«غير قابل للصيانة» (لا تعديل ولا تغيير حالة ولا نقل)</summary>
        public int StatusStage { get; set; }
        public bool IsClosed { get; set; }

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
    /// <summary>طلب تحويل طلب صيانة</summary>
    public class MaintenanceTransferDto
    {
        public int Id { get; set; }
        public int MaintenanceRequestId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public int? SuggestedUserId { get; set; }
        public string? SuggestedUserName { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public string? DecidedByName { get; set; }
        public string? NewUserName { get; set; }
        public string? DecisionNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DecidedAt { get; set; }
    }

    /// <summary>نتيجة البحث عن عميل موظف (مطابقة تامة) — بيانات دنيا فقط</summary>
    public class MaintenanceClientDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    /// <summary>طلب صيانة كما يراه العميل الموظف («أجهزتي في الصيانة») — بلا بيانات داخلية</summary>
    public class MyMaintenanceRequestDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceTypeName { get; set; } = string.Empty;
        public string DeviceCompanyName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string DamageTypeName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    public class TechnicianOptionDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}
