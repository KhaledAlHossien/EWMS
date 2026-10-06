using System.Globalization;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;

namespace Application.Features.DeviceInventory
{
    /// <summary>
    /// توثيق الأجهزة (مراجعة 2026-10-05): مصدر واحد للتحقق والتحويل وسجل التغييرات.
    /// الصلاحيات: ViewDevices/CreateDevice/EditDevice/DeleteDevice (سياسات المتحكمات)، وRevealDevicePasswords لكلمات السر.
    /// كلمة السر لا تُرسل في أي قائمة؛ تُقرأ من DeviceSites/Password/{id} ويُسجَّل كل إظهار ونسخ.
    /// </summary>
    public static class DeviceInventoryRules
    {
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

    // ════════════════════ التحقق (مصدر واحد للإضافة والتعديل والاستيراد) ════════════════════

    public class SiteRequestValidator : AbstractValidator<SiteRequestDto>
    {
        public SiteRequestValidator()
        {
            RuleFor(x => x.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("اسم الموقع مطلوب")
                .MaximumLength(100).WithMessage("اسم الموقع لا يتجاوز 100 حرف");
            RuleFor(x => x.Description).MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
            RuleFor(x => x.ContactName).MaximumLength(100).WithMessage("اسم المسؤول لا يتجاوز 100 حرف");
            RuleFor(x => x.ContactPhone).Must(PhoneRules.IsValid)
                .WithMessage("رقم هاتف المسؤول غير صحيح — مثال: 0933123456 أو 0112345678");
            RuleFor(x => x.ResponsibleParty).MaximumLength(150).WithMessage("الجهة المسؤولة لا تتجاوز 150 حرفاً");
            GeoRules.CoordinatesRules(this, x => x.Latitude, x => x.Longitude);
        }
    }

    public class DeviceRequestValidator : AbstractValidator<DeviceRequestDto>
    {
        public DeviceRequestValidator()
        {
            RuleFor(x => x.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("اسم الجهاز مطلوب")
                .MaximumLength(100).WithMessage("اسم الجهاز لا يتجاوز 100 حرف");
            RuleFor(x => x.Model).MaximumLength(100).WithMessage("الموديل لا يتجاوز 100 حرف");
            RuleFor(x => x.Description).MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
            RuleFor(x => x.Category).MaximumLength(50).WithMessage("الفئة لا تتجاوز 50 حرفاً");
            RuleFor(x => x.Manufacturer).MaximumLength(100).WithMessage("الشركة المصنّعة لا تتجاوز 100 حرف");
        }
    }

    /// <summary>
    /// قواعد التركيب المشتركة. creating: كلمة السر مطلوبة عند الإضافة، وعند التعديل فارغة = تبقى الحالية.
    /// مجرّدة: التسجيل التلقائي للمدققات لا يستطيع تمرير المعامل — الاستخدام عبر الصنفين أدناه.
    /// </summary>
    public abstract class InstallationRequestValidatorBase : AbstractValidator<InstallationRequestDto>
    {
        protected InstallationRequestValidatorBase(bool creating)
        {
            RuleFor(x => x.DeviceId).GreaterThan(0).WithMessage("يجب اختيار جهاز");
            RuleFor(x => x.SiteId).GreaterThan(0).WithMessage("يجب اختيار موقع");
            RuleFor(x => x.Ip).Must(NetworkRules.IsIpv4).WithMessage("عنوان الـ IP غير صحيح (مثال 192.168.1.10، بلا أصفار بادئة)");
            RuleFor(x => x.SubnetMask).Must(NetworkRules.IsSubnetMask)
                .WithMessage("قناع الشبكة غير صحيح — آحاد متصلة ثم أصفار، مثل 255.255.255.0");
            RuleFor(x => x.Gateway)
                .Must(NetworkRules.IsIpv4).WithMessage("عنوان البوابة غير صحيح")
                .Must((dto, gateway) => !NetworkRules.IsIpv4(dto.Ip) || !NetworkRules.IsSubnetMask(dto.SubnetMask) || NetworkRules.SameSubnet(dto.Ip, gateway, dto.SubnetMask))
                .WithMessage("البوابة ليست في شبكة الجهاز (بحسب الـ IP وقناع الشبكة)")
                .When(x => !string.IsNullOrWhiteSpace(x.Gateway));
            RuleFor(x => x.UserName).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("اسم المستخدم مطلوب")
                .MaximumLength(100).WithMessage("اسم المستخدم لا يتجاوز 100 حرف");
            if (creating) RuleFor(x => x.Pass).NotEmpty().WithMessage("كلمة السر مطلوبة");
            RuleFor(x => x.Pass).MaximumLength(200).WithMessage("كلمة السر لا تتجاوز 200 حرف");
            RuleFor(x => x.SN).MaximumLength(100).WithMessage("الرقم التسلسلي لا يتجاوز 100 حرف");
            RuleFor(x => x.InstallLocation).MaximumLength(300).WithMessage("مكان التركيب لا يتجاوز 300 حرف");
            RuleFor(x => x.Note).MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف");
            RuleFor(x => x.MacAddress).Must(NetworkRules.IsMac).WithMessage("عنوان MAC غير صحيح (12 خانة سداسية، مثل AA:BB:CC:DD:EE:FF)");
            RuleFor(x => x.Port).MaximumLength(50).WithMessage("المنفذ لا يتجاوز 50 حرفاً");
            RuleFor(x => x.Vlan).InclusiveBetween(1, 4094).WithMessage("رقم الـ VLAN بين 1 و4094").When(x => x.Vlan != null);
            RuleFor(x => x.Firmware).MaximumLength(100).WithMessage("إصدار البرنامج الثابت لا يتجاوز 100 حرف");
            RuleFor(x => x.InstallDate).Must(d => d == null || d.Value.Date <= DateTime.Today).WithMessage("تاريخ التركيب لا يكون في المستقبل");
            RuleFor(x => x.Status).Must(s => Enum.IsDefined(typeof(InstallationStatus), s)).WithMessage("حالة التركيب غير صحيحة");
        }
    }

    /// <summary>تركيب جديد (وأسطر الاستيراد): كلمة السر مطلوبة</summary>
    public class NewInstallationValidator : InstallationRequestValidatorBase
    {
        public NewInstallationValidator() : base(creating: true) { }
    }

    /// <summary>تعديل تركيب: كلمة السر الفارغة تُبقي الحالية</summary>
    public class ExistingInstallationValidator : InstallationRequestValidatorBase
    {
        public ExistingInstallationValidator() : base(creating: false) { }
    }
}
