namespace Application.DTOs.Request
{
    // ==================== توثيق الأجهزة (JSON — نفس الـ DTO للإضافة والتعديل) ====================
    // RowVersion: يُعاد كما وصل في الاستجابة عند التعديل؛ إن تغيّر السجل منذ فتحه يُرفض الحفظ برسالة واضحة.

    public class SiteRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ResponsibleParty { get; set; } = string.Empty;
        public byte[]? RowVersion { get; set; }
    }

    public class DeviceRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public byte[]? RowVersion { get; set; }
    }

    public class InstallationRequestDto
    {
        public int DeviceId { get; set; }
        public int SiteId { get; set; }
        public string Ip { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        /// <summary>مطلوبة عند الإضافة؛ عند التعديل فارغة = تبقى كلمة السر الحالية</summary>
        public string Pass { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string SN { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public int? Vlan { get; set; }
        public string Firmware { get; set; } = string.Empty;
        public DateTime? InstallDate { get; set; }
        /// <summary>1 يعمل، 2 معطّل، 3 أُزيل</summary>
        public int Status { get; set; } = 1;
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>بحث التركيبات (في الخادم، مقسّم صفحات)</summary>
    public class InstallationFilterDto
    {
        /// <summary>يحتوي في: IP، الجهاز، الموديل، الرقم التسلسلي، المستخدم، الموقع، مكان التركيب، MAC، الملاحظات</summary>
        public string? Q { get; set; }
        public string? GovernorateCode { get; set; }
        public int? SiteId { get; set; }
        public int? DeviceId { get; set; }
        public int? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
