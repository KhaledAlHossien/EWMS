using System.Globalization;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory
{
    public record SearchInstallationsQuery(InstallationFilterDto Filter) : IRequest<PagedResultDto<DeviceSiteResponseDto>>;
    public record GetInstallationByIdQuery(int Id) : IRequest<DeviceSiteResponseDto>;
    public record GetIpInUseQuery(int SiteId, string Ip, int? ExcludeId) : IRequest<List<IpInUseDto>>;
    public record CreateInstallationCommand(InstallationRequestDto Dto) : IRequest<DeviceSiteResponseDto>;
    public record UpdateInstallationCommand(int Id, InstallationRequestDto Dto) : IRequest<DeviceSiteResponseDto>;
    public record DeleteInstallationCommand(int Id) : IRequest<Unit>;
    /// <summary>Copy = نسخ (وإلا إظهار) — يُسجَّل كل منهما</summary>
    public record RevealDevicePasswordQuery(int Id, bool Copy) : IRequest<DevicePasswordDto>;
    public record VerifyInstallationCommand(int Id) : IRequest<DeviceSiteResponseDto>;
    public record GetDeviceInventoryHistoryQuery(DeviceInventoryEntity Type, int Id, int Page, int PageSize) : IRequest<PagedResultDto<DeviceInventoryLogDto>>;
    public record ExportInstallationsQuery(InstallationFilterDto Filter) : IRequest<byte[]>;
    public record GetImportTemplateQuery : IRequest<byte[]>;
    public record ImportInstallationsCommand(Stream File, bool DryRun) : IRequest<InstallationImportReportDto>;

    public class CreateInstallationCommandValidator : AbstractValidator<CreateInstallationCommand>
    {
        public CreateInstallationCommandValidator() => RuleFor(x => x.Dto).SetValidator(new NewInstallationValidator());
    }

    public class UpdateInstallationCommandValidator : AbstractValidator<UpdateInstallationCommand>
    {
        public UpdateInstallationCommandValidator() => RuleFor(x => x.Dto).SetValidator(new ExistingInstallationValidator());
    }

    public class InstallationHandler :
        IRequestHandler<SearchInstallationsQuery, PagedResultDto<DeviceSiteResponseDto>>,
        IRequestHandler<GetInstallationByIdQuery, DeviceSiteResponseDto>,
        IRequestHandler<GetIpInUseQuery, List<IpInUseDto>>,
        IRequestHandler<CreateInstallationCommand, DeviceSiteResponseDto>,
        IRequestHandler<UpdateInstallationCommand, DeviceSiteResponseDto>,
        IRequestHandler<DeleteInstallationCommand, Unit>,
        IRequestHandler<RevealDevicePasswordQuery, DevicePasswordDto>,
        IRequestHandler<VerifyInstallationCommand, DeviceSiteResponseDto>,
        IRequestHandler<GetDeviceInventoryHistoryQuery, PagedResultDto<DeviceInventoryLogDto>>,
        IRequestHandler<ExportInstallationsQuery, byte[]>,
        IRequestHandler<GetImportTemplateQuery, byte[]>,
        IRequestHandler<ImportInstallationsCommand, InstallationImportReportDto>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IDeviceService _devices;
        private readonly ISiteService _sites;
        private readonly IDevicePasswordProtector _protector;
        private readonly IDeviceInventoryLogService _log;
        private readonly IDeviceSpreadsheet _spreadsheet;
        private readonly ICurrentUserService _currentUser;

        public InstallationHandler(
            IDeviceSiteService installations, IDeviceService devices, ISiteService sites, IDevicePasswordProtector protector,
            IDeviceInventoryLogService log, IDeviceSpreadsheet spreadsheet, ICurrentUserService currentUser)
        {
            _installations = installations;
            _devices = devices;
            _sites = sites;
            _protector = protector;
            _log = log;
            _spreadsheet = spreadsheet;
            _currentUser = currentUser;
        }

        private async Task<DeviceSiteResponseDto> DtoAsync(int id) =>
            ToDto(await _installations.GetRowAsync(id) ?? throw new KeyNotFoundException("التركيب غير موجود"));

        private async Task<DeviceSite> LoadAsync(int id) =>
            await _installations.GetByIdAsync(id) ?? throw new KeyNotFoundException("التركيب غير موجود");

        // ───────── القراءة ─────────

        public async Task<PagedResultDto<DeviceSiteResponseDto>> Handle(SearchInstallationsQuery request, CancellationToken ct)
        {
            var (page, pageSize) = Paging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _installations.SearchAsync(request.Filter, page, pageSize);
            return new PagedResultDto<DeviceSiteResponseDto> { Items = items.Select(ToDto).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
        }

        public Task<DeviceSiteResponseDto> Handle(GetInstallationByIdQuery request, CancellationToken ct) => DtoAsync(request.Id);

        public async Task<List<IpInUseDto>> Handle(GetIpInUseQuery request, CancellationToken ct)
        {
            if (request.SiteId <= 0 || !NetworkRules.IsIpv4(request.Ip)) return [];
            return (await _installations.IpInUseAsync(request.SiteId, request.Ip.Trim(), request.ExcludeId))
                .Select(i => new IpInUseDto { Id = i.Id, DeviceName = i.Device?.Name ?? string.Empty, InstallLocation = i.InstallLocation })
                .ToList();
        }

        public async Task<PagedResultDto<DeviceInventoryLogDto>> Handle(GetDeviceInventoryHistoryQuery request, CancellationToken ct)
        {
            var (page, pageSize) = Paging(request.Page, request.PageSize);
            var (items, total) = await _log.GetAsync(request.Type, request.Id, page, pageSize);
            return new PagedResultDto<DeviceInventoryLogDto> { Items = items.Select(ToDto).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
        }

        // ───────── الكتابة ─────────

        /// <summary>الحقول المنظّفة كما تُحفظ (بلا كلمة السر — تُعالج منفصلة)</summary>
        private static void Apply(InstallationRequestDto dto, DeviceSite i)
        {
            i.DeviceId = dto.DeviceId;
            i.SiteId = dto.SiteId;
            i.Ip = Clean(dto.Ip);
            i.SubnetMask = Clean(dto.SubnetMask);
            i.Gateway = Clean(dto.Gateway);
            i.UserName = Clean(dto.UserName);
            i.Note = Clean(dto.Note);
            i.SN = Clean(dto.SN);
            i.InstallLocation = Clean(dto.InstallLocation);
            i.MacAddress = NetworkRules.NormalizeMac(dto.MacAddress);
            i.Port = Clean(dto.Port);
            i.Vlan = dto.Vlan;
            i.Firmware = Clean(dto.Firmware);
            i.InstallDate = dto.InstallDate?.Date;
            i.Status = (InstallationStatus)dto.Status;
        }

        private async Task<(Device Device, Site Site)> ReferencesAsync(int deviceId, int siteId) =>
            (await _devices.GetByIdAsync(deviceId) ?? throw new KeyNotFoundException("الجهاز المحدد غير موجود"),
             await _sites.GetByIdAsync(siteId) ?? throw new KeyNotFoundException("الموقع المحدد غير موجود"));

        public async Task<DeviceSiteResponseDto> Handle(CreateInstallationCommand request, CancellationToken ct)
        {
            var (device, site) = await ReferencesAsync(request.Dto.DeviceId, request.Dto.SiteId);

            // الجهاز نوع/موديل قابل للتكرار: يمكن تركيب نفس الجهاز أكثر من مرة في نفس الموقع — قرار المستخدم 2026-09-29
            var installation = new DeviceSite { DeviceId = device.Id, SiteId = site.Id };
            Apply(request.Dto, installation);
            installation.Pass = _protector.Protect(request.Dto.Pass);
            await _installations.AddAsync(installation);

            await _log.AddAsync(Log(DeviceInventoryEntity.Installation, installation.Id, DeviceInventoryAction.Created, _currentUser.UserId,
                Title(installation, device.Name, site.Name), $"الحالة: {StatusAr(installation.Status)}" + (installation.SN.Length > 0 ? $"\nالرقم التسلسلي: {installation.SN}" : "")));
            return await DtoAsync(installation.Id);
        }

        public async Task<DeviceSiteResponseDto> Handle(UpdateInstallationCommand request, CancellationToken ct)
        {
            var installation = await LoadAsync(request.Id);
            var (device, site) = await ReferencesAsync(request.Dto.DeviceId, request.Dto.SiteId);

            var (oldDevice, oldSite) = (installation.Device?.Name, installation.Site?.Name);
            var before = new DeviceSite
            {
                DeviceId = installation.DeviceId, SiteId = installation.SiteId, Ip = installation.Ip, SubnetMask = installation.SubnetMask,
                Gateway = installation.Gateway, UserName = installation.UserName, Note = installation.Note, SN = installation.SN,
                InstallLocation = installation.InstallLocation, MacAddress = installation.MacAddress, Port = installation.Port, Vlan = installation.Vlan,
                Firmware = installation.Firmware, InstallDate = installation.InstallDate, Status = installation.Status
            };

            Apply(request.Dto, installation);
            var passwordChanged = !string.IsNullOrEmpty(request.Dto.Pass) && request.Dto.Pass != _protector.Unprotect(installation.Pass);
            if (passwordChanged) installation.Pass = _protector.Protect(request.Dto.Pass);
            await _installations.UpdateAsync(installation, request.Dto.RowVersion);

            var diff = new Diff()
                .Add("الجهاز", oldDevice, device.Name)
                .Add("الموقع", oldSite, site.Name)
                .Add("IP", before.Ip, installation.Ip)
                .Add("قناع الشبكة", before.SubnetMask, installation.SubnetMask)
                .Add("البوابة", before.Gateway, installation.Gateway)
                .Add("المستخدم", before.UserName, installation.UserName)
                .Add("الرقم التسلسلي", before.SN, installation.SN)
                .Add("مكان التركيب", before.InstallLocation, installation.InstallLocation)
                .Add("MAC", before.MacAddress, installation.MacAddress)
                .Add("المنفذ", before.Port, installation.Port)
                .Add("VLAN", before.Vlan, installation.Vlan)
                .Add("البرنامج الثابت", before.Firmware, installation.Firmware)
                .Add("تاريخ التركيب", before.InstallDate, installation.InstallDate)
                .Add("الحالة", StatusAr(before.Status), StatusAr(installation.Status))
                .Add("الملاحظات", before.Note, installation.Note);
            if (passwordChanged) diff.Note("كلمة السر: تغيّرت");
            if (diff.Any)
                await _log.AddAsync(Log(DeviceInventoryEntity.Installation, installation.Id, DeviceInventoryAction.Updated, _currentUser.UserId,
                    Title(installation, device.Name, site.Name), diff.ToString()));

            return await DtoAsync(installation.Id);
        }

        public async Task<Unit> Handle(DeleteInstallationCommand request, CancellationToken ct)
        {
            var installation = await LoadAsync(request.Id);
            var title = Title(installation, installation.Device?.Name ?? "", installation.Site?.Name ?? "");
            await _installations.DeleteAsync(installation);
            await _log.AddAsync(Log(DeviceInventoryEntity.Installation, request.Id, DeviceInventoryAction.Deleted, _currentUser.UserId, title,
                installation.SN.Length > 0 ? $"الرقم التسلسلي: {installation.SN}" : ""));
            return Unit.Value;
        }

        public async Task<DevicePasswordDto> Handle(RevealDevicePasswordQuery request, CancellationToken ct)
        {
            var installation = await LoadAsync(request.Id);
            if (string.IsNullOrEmpty(installation.Pass))
                throw new InvalidOperationException("لا توجد كلمة سر محفوظة لهذا التركيب");

            var password = _protector.Unprotect(installation.Pass);
            await _log.AddAsync(Log(DeviceInventoryEntity.Installation, installation.Id,
                request.Copy ? DeviceInventoryAction.PasswordCopied : DeviceInventoryAction.PasswordRevealed, _currentUser.UserId,
                Title(installation, installation.Device?.Name ?? "", installation.Site?.Name ?? "")));
            return new DevicePasswordDto { Password = password };
        }

        public async Task<DeviceSiteResponseDto> Handle(VerifyInstallationCommand request, CancellationToken ct)
        {
            var installation = await LoadAsync(request.Id);
            installation.LastVerifiedAt = DateTime.Today;
            await _installations.UpdateAsync(installation, null);
            await _log.AddAsync(Log(DeviceInventoryEntity.Installation, installation.Id, DeviceInventoryAction.Verified, _currentUser.UserId,
                Title(installation, installation.Device?.Name ?? "", installation.Site?.Name ?? "")));
            return await DtoAsync(installation.Id);
        }

        // ───────── Excel ─────────

        public async Task<byte[]> Handle(ExportInstallationsQuery request, CancellationToken ct) =>
            _spreadsheet.ExportInstallations(await _installations.ExportAsync(request.Filter, MaxExportRows));

        public Task<byte[]> Handle(GetImportTemplateQuery request, CancellationToken ct) => Task.FromResult(_spreadsheet.ImportTemplate());

        private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy"];

        /// <summary>
        /// استيراد التركيبات (قرار المستخدم 2026-10-05): الموقع يجب أن يكون موجوداً (يحتاج إحداثيات) وإلا يُرفض السطر،
        /// والجهاز (الاسم + الموديل) يُضاف إلى الكتالوج إن لم يوجد. المعاينة لا تحفظ شيئاً؛ التنفيذ يحفظ الأسطر الصالحة فقط في معاملة واحدة.
        /// </summary>
        public async Task<InstallationImportReportDto> Handle(ImportInstallationsCommand request, CancellationToken ct)
        {
            var rows = _spreadsheet.ReadInstallations(request.File, MaxImportRows);
            var report = new InstallationImportReportDto { DryRun = request.DryRun, Total = rows.Count };
            if (rows.Count == 0) throw new InvalidOperationException("الملف لا يحتوي أسطراً — استخدم قالب الاستيراد");

            var sites = await _sites.GetByNamesAsync(rows.Select(r => Clean(r.Site)));
            var knownDevices = new Dictionary<(string, string), Device>();
            var newDevices = new Dictionary<(string, string), Device>();
            var valid = new List<(InstallationSheetRow Row, DeviceSite Installation, Device Device, Site Site)>();
            var seenIps = new HashSet<(int, string)>();
            var validator = new NewInstallationValidator();
            var deviceValidator = new DeviceRequestValidator();

            foreach (var row in rows)
            {
                var line = new InstallationImportRowDto { Row = row.Row, Site = Clean(row.Site), Device = Clean(row.Device), Ip = Clean(row.Ip) };
                report.Rows.Add(line);
                var errors = new List<string>();

                if (!sites.TryGetValue(line.Site, out var site))
                    errors.Add(line.Site.Length == 0 ? "اسم الموقع مطلوب" : $"الموقع «{line.Site}» غير موجود — أضفه من صفحة المواقع أولاً");

                int? vlan = null;
                if (Clean(row.Vlan).Length > 0)
                {
                    if (int.TryParse(Clean(row.Vlan), out var v)) vlan = v; else errors.Add("رقم الـ VLAN غير صحيح");
                }
                DateTime? installDate = null;
                if (Clean(row.InstallDate).Length > 0)
                {
                    if (DateTime.TryParseExact(Clean(row.InstallDate), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) installDate = d;
                    else errors.Add("تاريخ التركيب غير مفهوم (مثال 2026-10-05)");
                }
                var status = ParseStatus(row.Status);
                if (status == null) errors.Add("الحالة غير مفهومة (يعمل / معطّل / أُزيل)");

                var dto = new InstallationRequestDto
                {
                    DeviceId = 1, SiteId = 1,   // يُتحقق منهما أعلاه وأدناه
                    Ip = row.Ip, SubnetMask = row.SubnetMask, Gateway = row.Gateway, UserName = row.UserName, Pass = row.Pass,
                    Note = row.Note, SN = row.SN, InstallLocation = row.InstallLocation, MacAddress = row.MacAddress, Port = row.Port,
                    Vlan = vlan, Firmware = row.Firmware, InstallDate = installDate, Status = (int)(status ?? InstallationStatus.Active)
                };
                errors.AddRange((await validator.ValidateAsync(dto, ct)).Errors.Select(e => e.ErrorMessage));

                // الجهاز: موجود في الكتالوج، أو جديد يُضاف
                var deviceDto = new DeviceRequestDto { Name = row.Device, Model = row.Model, Category = row.Category, Manufacturer = row.Manufacturer };
                var deviceErrors = (await deviceValidator.ValidateAsync(deviceDto, ct)).Errors.Select(e => e.ErrorMessage).ToList();
                errors.AddRange(deviceErrors);
                var key = (Clean(row.Device), Clean(row.Model));
                Device? device = null;
                if (deviceErrors.Count == 0)
                {
                    if (!knownDevices.TryGetValue(key, out device) && !newDevices.TryGetValue(key, out device))
                    {
                        device = await _devices.FindAsync(key.Item1, key.Item2);
                        if (device != null) knownDevices[key] = device;
                        else
                        {
                            device = new Device { Name = key.Item1, Model = key.Item2, Category = Clean(row.Category), Manufacturer = Clean(row.Manufacturer) };
                            newDevices[key] = device;
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    line.Message = string.Join(" — ", errors.Distinct());
                    continue;
                }

                var installation = new DeviceSite { DeviceId = device!.Id, SiteId = site!.Id };
                Apply(dto, installation);
                installation.DeviceId = device.Id;
                installation.SiteId = site.Id;
                // الجهاز الجديد يُحفظ مع التركيب (معرّفه يُعرف عند الحفظ)؛ الموجود يُربط بمعرّفه فقط
                if (device.Id == 0) installation.Device = device;
                installation.Pass = _protector.Protect(row.Pass);

                // تكرار الـ IP: تنبيه فقط (قرار المستخدم 2026-09-29)
                var warnings = new List<string>();
                if (!seenIps.Add((site.Id, installation.Ip))) warnings.Add("الـ IP مكرر في الملف لنفس الموقع");
                else if ((await _installations.IpInUseAsync(site.Id, installation.Ip, null)).Count > 0) warnings.Add("الـ IP مستخدم في هذا الموقع مسبقاً");
                if (newDevices.ContainsKey(key)) warnings.Add("جهاز جديد يُضاف إلى الكتالوج");

                line.Ok = true;
                line.Message = string.Join(" — ", warnings);
                valid.Add((row, installation, device, site));
            }

            report.Valid = valid.Count;
            var usedNew = newDevices.Values.Where(d => valid.Any(v => ReferenceEquals(v.Device, d))).ToList();
            report.NewDevices = usedNew.Select(Title).ToList();
            if (request.DryRun || valid.Count == 0) return report;

            await _installations.ImportAsync(usedNew, valid.Select(v => v.Installation).ToList());
            report.Imported = valid.Count;

            var userId = _currentUser.UserId;
            await _log.AddRangeAsync(
                usedNew.Select(d => Log(DeviceInventoryEntity.Device, d.Id, DeviceInventoryAction.Imported, userId, Title(d)))
                .Concat(valid.Select(v => Log(DeviceInventoryEntity.Installation, v.Installation.Id, DeviceInventoryAction.Imported, userId,
                    Title(v.Installation, v.Device.Name, v.Site.Name), $"السطر {v.Row.Row} من ملف الاستيراد"))));
            return report;
        }
    }
}
