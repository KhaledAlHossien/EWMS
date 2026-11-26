using System.Globalization;
using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.DeviceInventory
{
    /// <summary>
    /// توثيق الأجهزة (مراجعة 2026-10-05): مصدر واحد للتحقق والتحويل وسجل التغييرات.
    /// الصلاحيات: ViewDevices/CreateDevice/EditDevice/DeleteDevice (سياسات المتحكمات)، وRevealDevicePasswords لكلمات السر.
    /// كلمة السر لا تُرسل في أي قائمة؛ تُقرأ من DeviceSites/Password/{id} ويُسجَّل كل إظهار ونسخ.
    /// المواقع سجل مشترك بصلاحياتها (ViewSites…)، والتركيب ملك قسم من أضافه وفرعه (2026-10-11) — ScopeFor.
    /// </summary>
    public static class DeviceInventoryRules
    {
        /// <summary>
        /// التركيبات التي يراها ويديرها المستخدم: كلها لنظرة المؤسسة، وتركيبات فرعه مع ViewBranchDevices،
        /// وإلا تركيبات قسمه. والصلاحية (عرض/تعديل/حذف/إظهار كلمة السر) تحدد العملية داخل هذا الحد.
        /// </summary>
        public static InstallationScope ScopeFor(Viewer viewer)
        {
            if (viewer.IsOrganizationWide) return InstallationScope.Everything;
            if (viewer.Has(AppPermissions.ViewBranchDevices) && viewer.User.BranchId is int branchId)
                return new InstallationScope(false, branchId, null);
            if (viewer.User.DepartmentId is int departmentId)
                return new InstallationScope(false, null, departmentId);
            return InstallationScope.Nothing;
        }

        public static void EnsureInScope(Viewer viewer, DeviceSite installation)
        {
            if (!ScopeFor(viewer).Includes(installation))
                throw new UnauthorizedAccessException("هذا التركيب يتبع قسماً أو فرعاً آخر");
        }

        public const int MaxPageSize = 200;
        public const int MaxExportRows = 20_000;
        public const int MaxImportRows = 2_000;

        public static string Clean(string? value) => (value ?? string.Empty).Trim();

        public static (int Page, int PageSize) Paging(int page, int pageSize) =>
            (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

        public static string StatusAr(InstallationStatus status) => status switch
        {
            InstallationStatus.Active => "يعمل",
            InstallationStatus.Faulty => "معطّل",
            InstallationStatus.Removed => "أُزيل",
            _ => status.ToString()
        };

        /// <summary>الحالة من نص Excel أو رقم: يعمل/معطل/أزيل (أو 1/2/3)، والفارغ = يعمل</summary>
        public static InstallationStatus? ParseStatus(string? text)
        {
            var t = Clean(text).Replace("ّ", "").Replace("أ", "ا").Replace("ُ", "");
            return t switch
            {
                "" or "1" or "يعمل" => InstallationStatus.Active,
                "2" or "معطل" => InstallationStatus.Faulty,
                "3" or "ازيل" or "مزال" => InstallationStatus.Removed,
                _ => null
            };
        }

        public static string ActionAr(DeviceInventoryAction action) => action switch
        {
            DeviceInventoryAction.Created => "أضاف",
            DeviceInventoryAction.Updated => "عدّل",
            DeviceInventoryAction.Deleted => "حذف",
            DeviceInventoryAction.PasswordRevealed => "أظهر كلمة السر",
            DeviceInventoryAction.PasswordCopied => "نسخ كلمة السر",
            DeviceInventoryAction.Verified => "تحقق من البيانات",
            DeviceInventoryAction.Imported => "استورد من Excel",
            _ => action.ToString()
        };

        // ───────── عناوين مقروءة للسجلات (تبقى في السجل بعد الحذف) ─────────
        public static string Title(Site s) => s.Name;
        public static string Title(Device d) => string.IsNullOrEmpty(d.Model) ? d.Name : $"{d.Name} ({d.Model})";
        public static string Title(DeviceSite i, string deviceName, string siteName) => $"{deviceName} في {siteName} — {i.Ip}";

        public static DeviceInventoryLog Log(DeviceInventoryEntity type, int id, DeviceInventoryAction action, int userId, string title, string details = "") => new()
        {
            EntityType = type,
            EntityId = id,
            Action = action,
            Title = title.Length > 300 ? title[..300] : title,
            Details = details.Length > 4000 ? details[..4000] : details,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        /// <summary>سطر لكل حقل تغيّر: «الحقل: قديم ← جديد» (الفارغ يظهر «—»)</summary>
        public sealed class Diff
        {
            private readonly List<string> _lines = [];
            public Diff Add(string label, object? before, object? after)
            {
                string Show(object? v) => v switch
                {
                    null => "—",
                    DateTime d => d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    double n => n.ToString("0.#####", CultureInfo.InvariantCulture),
                    _ => string.IsNullOrWhiteSpace(v.ToString()) ? "—" : v.ToString()!
                };
                var (b, a) = (Show(before), Show(after));
                if (b != a) _lines.Add($"{label}: {b} ← {a}");
                return this;
            }
            public Diff Note(string line) { _lines.Add(line); return this; }
            public bool Any => _lines.Count > 0;
            public override string ToString() => string.Join("\n", _lines);
        }

        // ───────── التحويل إلى DTO ─────────
        public static SiteResponseDto ToDto(SiteWithCounts row) => new()
        {
            Id = row.Site.Id,
            Name = row.Site.Name,
            Description = row.Site.Description,
            Latitude = row.Site.Latitude,
            Longitude = row.Site.Longitude,
            GovernorateCode = row.Site.GovernorateCode,
            GovernorateName = Governorates.NameOf(row.Site.GovernorateCode),
            ContactName = row.Site.ContactName,
            ContactPhone = row.Site.ContactPhone,
            ResponsibleParty = row.Site.ResponsibleParty,
            InstallationsCount = row.Installations,
            ActiveInstallationsCount = row.Active,
            RowVersion = row.Site.RowVersion
        };

        public static DeviceResponseDto ToDto(DeviceWithCount row) => new()
        {
            Id = row.Device.Id,
            Name = row.Device.Name,
            Model = row.Device.Model,
            Description = row.Device.Description,
            Category = row.Device.Category,
            Manufacturer = row.Device.Manufacturer,
            InstallationsCount = row.Installations,
            RowVersion = row.Device.RowVersion
        };

        public static DeviceSiteResponseDto ToDto(InstallationRow row)
        {
            var i = row.Installation;
            return new DeviceSiteResponseDto
            {
                Id = i.Id,
                DeviceId = i.DeviceId,
                DeviceName = i.Device?.Name ?? string.Empty,
                DeviceModel = i.Device?.Model ?? string.Empty,
                DeviceCategory = i.Device?.Category ?? string.Empty,
                SiteId = i.SiteId,
                SiteName = i.Site?.Name ?? string.Empty,
                GovernorateCode = i.Site?.GovernorateCode ?? string.Empty,
                GovernorateName = Governorates.NameOf(i.Site?.GovernorateCode ?? string.Empty),
                Ip = i.Ip,
                SubnetMask = i.SubnetMask,
                Gateway = i.Gateway,
                UserName = i.UserName,
                HasPassword = !string.IsNullOrEmpty(i.Pass),
                Note = i.Note,
                SN = i.SN,
                InstallLocation = i.InstallLocation,
                MacAddress = i.MacAddress,
                Port = i.Port,
                Vlan = i.Vlan,
                Firmware = i.Firmware,
                InstallDate = i.InstallDate,
                LastVerifiedAt = i.LastVerifiedAt,
                Status = (int)i.Status,
                StatusAr = StatusAr(i.Status),
                DuplicateIp = row.DuplicateIp,
                BranchId = i.BranchId,
                BranchName = i.Branch?.Name ?? string.Empty,
                DepartmentId = i.DepartmentId,
                DepartmentName = i.Department?.Name ?? string.Empty,
                RowVersion = i.RowVersion
            };
        }

        public static DeviceInventoryLogDto ToDto(DeviceInventoryLog l) => new()
        {
            Id = l.Id,
            Action = (int)l.Action,
            ActionAr = ActionAr(l.Action),
            Title = l.Title,
            Details = l.Details,
            UserName = l.User?.FullName ?? string.Empty,
            CreatedAt = l.CreatedAt
        };
    }
}
