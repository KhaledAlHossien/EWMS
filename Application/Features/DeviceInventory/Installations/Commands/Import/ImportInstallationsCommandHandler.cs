using System.Globalization;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Commands.Import
{
    public class ImportInstallationsCommandHandler : IRequestHandler<ImportInstallationsCommand, InstallationImportReportDto>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IDeviceService _devices;
        private readonly ISiteService _sites;
        private readonly IDevicePasswordProtector _protector;
        private readonly IDeviceInventoryLogService _log;
        private readonly IDeviceSpreadsheet _spreadsheet;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly InstallationSupport _support;

        public ImportInstallationsCommandHandler(IDeviceSiteService installations, IDeviceService devices, ISiteService sites, IDevicePasswordProtector protector, IDeviceInventoryLogService log, IDeviceSpreadsheet spreadsheet, ICurrentUserService currentUser, IUserService users, IUserPermissionService permissions, InstallationSupport support)
        {
            _installations = installations;
            _devices = devices;
            _sites = sites;
            _protector = protector;
            _log = log;
            _spreadsheet = spreadsheet;
            _currentUser = currentUser;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        /// <summary>
        /// استيراد التركيبات (قرار المستخدم 2026-10-05): الموقع يجب أن يكون موجوداً (يحتاج إحداثيات) وإلا يُرفض السطر،
        /// والجهاز (الاسم + الموديل) يُضاف إلى الكتالوج إن لم يوجد. المعاينة لا تحفظ شيئاً؛ التنفيذ يحفظ الأسطر الصالحة فقط في معاملة واحدة.
        /// </summary>
        public async Task<InstallationImportReportDto> Handle(ImportInstallationsCommand request, CancellationToken ct)
        {
            var rows = _spreadsheet.ReadInstallations(request.File, MaxImportRows);
            var report = new InstallationImportReportDto { DryRun = request.DryRun, Total = rows.Count };
            if (rows.Count == 0) throw new InvalidOperationException("الملف لا يحتوي أسطراً — استخدم قالب الاستيراد");
            var viewer = await CurrentAsync();
            var (unitBranchId, unitDepartmentId) = await _support.UnitAsync(viewer, request.DepartmentId);
            var scope = ScopeFor(viewer);

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

                var installation = new DeviceSite { DeviceId = device!.Id, SiteId = site!.Id, BranchId = unitBranchId, DepartmentId = unitDepartmentId };
                InstallationSupport.Apply(dto, installation);
                installation.DeviceId = device.Id;
                installation.SiteId = site.Id;
                // الجهاز الجديد يُحفظ مع التركيب (معرّفه يُعرف عند الحفظ)؛ الموجود يُربط بمعرّفه فقط
                if (device.Id == 0) installation.Device = device;
                installation.Pass = _protector.Protect(row.Pass);

                // تكرار الـ IP: تنبيه فقط (قرار المستخدم 2026-09-29)
                var warnings = new List<string>();
                if (!seenIps.Add((site.Id, installation.Ip))) warnings.Add("الـ IP مكرر في الملف لنفس الموقع");
                else if ((await _installations.IpInUseAsync(site.Id, installation.Ip, null, scope)).Count > 0) warnings.Add("الـ IP مستخدم في هذا الموقع مسبقاً");
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

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy"];
    }
}
