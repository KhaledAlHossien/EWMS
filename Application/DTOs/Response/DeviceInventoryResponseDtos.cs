namespace Application.DTOs.Response
{
    // ==================== توثيق الأجهزة ====================

    public class SiteResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string GovernorateCode { get; set; } = string.Empty;
        public string GovernorateName { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ResponsibleParty { get; set; } = string.Empty;
        /// <summary>كل التركيبات، والتي حالتها «يعمل» — بدل تحميل التركيبات لعدّها</summary>
        public int InstallationsCount { get; set; }
        public int ActiveInstallationsCount { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class DeviceResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public int InstallationsCount { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>تركيب جهاز في موقع — بلا كلمة السر (تُجلب منفصلة بصلاحية الإظهار)</summary>
    public class DeviceSiteResponseDto
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string DeviceCategory { get; set; } = string.Empty;
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string GovernorateCode { get; set; } = string.Empty;
        public string GovernorateName { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool HasPassword { get; set; }
        public string Note { get; set; } = string.Empty;
        public string SN { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public int? Vlan { get; set; }
        public string Firmware { get; set; } = string.Empty;
        public DateTime? InstallDate { get; set; }
        public DateTime? LastVerifiedAt { get; set; }
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        /// <summary>تركيب آخر بنفس الـ IP في نفس الموقع (تنبيه فقط — قرار المستخدم 2026-09-29)</summary>
        public bool DuplicateIp { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>تركيب آخر يستخدم نفس الـ IP في الموقع (تنبيه النموذج أثناء الكتابة)</summary>
    public class IpInUseDto
    {
        public int Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
    }

    public class DevicePasswordDto
    {
        public string Password { get; set; } = string.Empty;
    }

    public class DeviceInventoryLogDto
    {
        public int Id { get; set; }
        public int Action { get; set; }
        public string ActionAr { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>تقرير استيراد ملف Excel (معاينة أو تنفيذ)</summary>
    public class InstallationImportReportDto
    {
        public bool DryRun { get; set; }
        public int Total { get; set; }
        public int Valid { get; set; }
        public int Imported { get; set; }
        /// <summary>أجهزة الكتالوج التي أُنشئت (أو ستُنشأ في المعاينة)</summary>
        public List<string> NewDevices { get; set; } = [];
        public List<InstallationImportRowDto> Rows { get; set; } = [];
    }

    public class InstallationImportRowDto
    {
        /// <summary>رقم السطر في الملف</summary>
        public int Row { get; set; }
        public bool Ok { get; set; }
        public string Site { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        /// <summary>سبب الرفض، أو تنبيه (مثل تكرار الـ IP) للسطر الصالح</summary>
        public string Message { get; set; } = string.Empty;
    }
}
