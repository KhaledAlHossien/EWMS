using System.Linq.Expressions;
using Application.DTOs.Request;
using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>تركيب مع علامة تكرار الـ IP في موقعه</summary>
    public sealed record InstallationRow(DeviceSite Installation, bool DuplicateIp);

    /// <summary>
    /// التركيبات التي يراها المستخدم (2026-10-11): كلها (نظرة المؤسسة)، أو تركيبات فرعه (ViewBranchDevices)،
    /// أو تركيبات قسمه. لا قسم ولا فرع = لا شيء. يحسبه DeviceInventoryRules.ScopeFor.
    /// </summary>
    public sealed record InstallationScope(bool All, int? BranchId, int? DepartmentId)
    {
        public static readonly InstallationScope Everything = new(true, null, null);
        public static readonly InstallationScope Nothing = new(false, null, null);

        public bool Includes(DeviceSite i) =>
            All || (BranchId != null && i.BranchId == BranchId) || (DepartmentId != null && i.DepartmentId == DepartmentId);

        /// <summary>نفس الشرط لاستعلامات EF</summary>
        public Expression<Func<DeviceSite, bool>> Predicate()
        {
            if (All) return ds => true;
            if (BranchId is int branchId) return ds => ds.BranchId == branchId;
            if (DepartmentId is int departmentId) return ds => ds.DepartmentId == departmentId;
            return ds => false;
        }
    }

    public interface IDeviceSiteService
    {
        /// <summary>مع الجهاز والموقع والقسم والفرع</summary>
        Task<DeviceSite?> GetByIdAsync(int id);
        Task<InstallationRow?> GetRowAsync(int id);

        /// <summary>بحث مقسّم صفحات ضمن حدّ المستخدم (مرتّب بالموقع ثم الجهاز ثم الـ IP)</summary>
        Task<(List<InstallationRow> Items, int TotalCount)> SearchAsync(InstallationFilterDto filter, InstallationScope scope, int page, int pageSize);
        /// <summary>كل نتائج البحث بلا تقسيم (للتصدير) — حد أقصى للحماية</summary>
        Task<List<InstallationRow>> ExportAsync(InstallationFilterDto filter, InstallationScope scope, int max);

        /// <summary>تركيبات أخرى بنفس الـ IP في الموقع (ضمن حدّ المستخدم)</summary>
        Task<List<DeviceSite>> IpInUseAsync(int siteId, string ip, int? excludeId, InstallationScope scope);

        /// <summary>حراسة حذف الفرع والقسم (المفتاحان Restrict)</summary>
        Task<bool> ExistsForBranchAsync(int branchId);
        Task<bool> ExistsForDepartmentAsync(int departmentId);

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
        /// <summary>مشفّرة بمفتاح قديم (DeviceInventory:OldPasswordKeys) لا بالحالي: تُعاد تشفيرها عند الإقلاع بعد تدوير المفتاح</summary>
        bool NeedsRotation(string stored);
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
