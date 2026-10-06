using Application.DTOs.Request;
using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>تركيب مع علامة تكرار الـ IP في موقعه</summary>
    public sealed record InstallationRow(DeviceSite Installation, bool DuplicateIp);

    public interface IDeviceSiteService
    {
        /// <summary>مع الجهاز والموقع</summary>
        Task<DeviceSite?> GetByIdAsync(int id);
        Task<InstallationRow?> GetRowAsync(int id);

        /// <summary>بحث مقسّم صفحات (الأحدث تحديثاً لا يهم: مرتّب بالموقع ثم الجهاز ثم الـ IP)</summary>
        Task<(List<InstallationRow> Items, int TotalCount)> SearchAsync(InstallationFilterDto filter, int page, int pageSize);
        /// <summary>كل نتائج البحث بلا تقسيم (للتصدير) — حد أقصى للحماية</summary>
        Task<List<InstallationRow>> ExportAsync(InstallationFilterDto filter, int max);

        /// <summary>تركيبات أخرى بنفس الـ IP في الموقع</summary>
        Task<List<DeviceSite>> IpInUseAsync(int siteId, string ip, int? excludeId);

        Task<DeviceSite> AddAsync(DeviceSite deviceSite);
        Task UpdateAsync(DeviceSite deviceSite, byte[]? rowVersion);
        Task DeleteAsync(DeviceSite deviceSite);
        /// <summary>استيراد: الأجهزة الجديدة ثم التركيبات في معاملة واحدة</summary>
        Task ImportAsync(List<Device> newDevices, List<DeviceSite> installations);
    }

    /// <summary>تشفير كلمات سر الأجهزة (AES-GCM بمفتاح الإعدادات DeviceInventory:PasswordKey)</summary>
    public interface IDevicePasswordProtector
    {
        string Protect(string plain);
        /// <summary>يفك المشفّرة، ويعيد النص القديم غير المشفّر كما هو (قبل الترحيل)</summary>
        string Unprotect(string stored);
        bool IsProtected(string stored);
    }

    public interface IDeviceInventoryLogService
    {
        Task AddAsync(DeviceInventoryLog log);
        Task AddRangeAsync(IEnumerable<DeviceInventoryLog> logs);
        Task<(List<DeviceInventoryLog> Items, int TotalCount)> GetAsync(DeviceInventoryEntity type, int entityId, int page, int pageSize);
        Task<bool> ExistsForUserAsync(int userId);
    }

    /// <summary>سطر من ملف استيراد التركيبات (النصوص كما في الخلايا)</summary>
    public sealed class InstallationSheetRow
    {
        public int Row { get; set; }
        public string Site { get; set; } = "";
        public string Device { get; set; } = "";
        public string Model { get; set; } = "";
        public string Category { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string SN { get; set; } = "";
        public string InstallLocation { get; set; } = "";
        public string Ip { get; set; } = "";
        public string SubnetMask { get; set; } = "";
        public string Gateway { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string Port { get; set; } = "";
        public string Vlan { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Pass { get; set; } = "";
        public string Firmware { get; set; } = "";
        public string InstallDate { get; set; } = "";
        public string Status { get; set; } = "";
        public string Note { get; set; } = "";
    }

    /// <summary>ملفات Excel لتوثيق الأجهزة (ClosedXML في Infrastructure)</summary>
    public interface IDeviceSpreadsheet
    {
        /// <summary>تصدير التركيبات — بلا كلمات السر أبداً</summary>
        byte[] ExportInstallations(IEnumerable<InstallationRow> rows);
        /// <summary>قالب الاستيراد: الأعمدة بالترتيب مع سطر مثال وورقة تعليمات</summary>
        byte[] ImportTemplate();
        /// <summary>يقرأ أسطر ورقة التركيبات (يتخطى الفارغة) — InvalidOperationException برسالة عربية إن لم يُقرأ الملف</summary>
        List<InstallationSheetRow> ReadInstallations(Stream file, int maxRows);
    }
}
