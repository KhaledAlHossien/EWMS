namespace Application.DTOs.Request
{
    // ==================== الجداول المساعدة (نفس الـ DTO للإضافة والتعديل) ====================

    public class DeviceTypeRequestDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class DeviceCompanyRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class DamageTypeRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class MaintenanceRequestStatusRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FFFFFF";
    }

    // ==================== طلب الصيانة ====================
    // الفني (UserId) والقسم لا يُرسلان: يُؤخذان من المستخدم الحالي عند الإنشاء
    public class SaveMaintenanceRequestDto
    {
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public int DeviceTypeId { get; set; }
        public int DamageTypeId { get; set; }
        public int DeviceCompanyId { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Accessories { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaintenanceRequestStatusId { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// بحث طلبات الصيانة — كل الحقول اختيارية وتُجمع بـ AND.
    /// SerialNumber/Model: "يبدأ بـ" (يستفيد من الفهرس)، ClientName: "يحتوي".
    /// </summary>
    public class MaintenanceRequestFilterDto
    {
        public string? SerialNumber { get; set; }
        public string? Model { get; set; }
        public string? ClientName { get; set; }
        public int? DeviceCompanyId { get; set; }
        public int? TechnicianId { get; set; }
        public int? DeviceTypeId { get; set; }
        public int? DamageTypeId { get; set; }
        public int? StatusId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // ==================== مهمة الصيانة ====================
    public class SaveMaintenanceTaskDto
    {
        public string TaskLocation { get; set; } = string.Empty;
        public string RequestingParty { get; set; } = string.Empty;
        public string RequiredWork { get; set; } = string.Empty;
        public string CompletedWorks { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
