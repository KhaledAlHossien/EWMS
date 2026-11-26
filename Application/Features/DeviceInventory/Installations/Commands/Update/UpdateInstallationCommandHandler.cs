using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Commands.Update
{
    public class UpdateInstallationCommandHandler : IRequestHandler<UpdateInstallationCommand, DeviceSiteResponseDto>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IDevicePasswordProtector _protector;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly InstallationSupport _support;

        public UpdateInstallationCommandHandler(IDeviceSiteService installations, IDevicePasswordProtector protector, IDeviceInventoryLogService log, ICurrentUserService currentUser, IUserService users, IUserPermissionService permissions, InstallationSupport support)
        {
            _installations = installations;
            _protector = protector;
            _log = log;
            _currentUser = currentUser;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<DeviceSiteResponseDto> Handle(UpdateInstallationCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var installation = await _support.LoadInScopeAsync(request.Id, viewer);
            var (device, site) = await _support.ReferencesAsync(request.Dto.DeviceId, request.Dto.SiteId);

            var (oldDevice, oldSite) = (installation.Device?.Name, installation.Site?.Name);
            var oldUnit = UnitName(installation.Branch?.Name, installation.Department?.Name);
            // نقل التركيب إلى قسم آخر: لمن يملك نظرة المؤسسة أو فرعه (ViewBranchDevices) فقط
            if (request.Dto.DepartmentId is int requested && requested != installation.DepartmentId)
            {
                var (branchId, departmentId) = await _support.UnitAsync(viewer, requested);
                installation.BranchId = branchId;
                installation.DepartmentId = departmentId;
            }
            var before = new DeviceSite
            {
                DeviceId = installation.DeviceId, SiteId = installation.SiteId, Ip = installation.Ip, SubnetMask = installation.SubnetMask,
                Gateway = installation.Gateway, UserName = installation.UserName, Note = installation.Note, SN = installation.SN,
                InstallLocation = installation.InstallLocation, MacAddress = installation.MacAddress, Port = installation.Port, Vlan = installation.Vlan,
                Firmware = installation.Firmware, InstallDate = installation.InstallDate, Status = installation.Status
            };

            InstallationSupport.Apply(request.Dto, installation);
            var passwordChanged = !string.IsNullOrEmpty(request.Dto.Pass) && request.Dto.Pass != _protector.Unprotect(installation.Pass);
            if (passwordChanged) installation.Pass = _protector.Protect(request.Dto.Pass);
            await _installations.UpdateAsync(installation, request.Dto.RowVersion);
            var saved = await _support.LoadAsync(installation.Id);

            var diff = new Diff()
                .Add("القسم", oldUnit, UnitName(saved.Branch?.Name, saved.Department?.Name))
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

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<DeviceSiteResponseDto> DtoAsync(int id) =>
            ToDto(await _installations.GetRowAsync(id) ?? throw new KeyNotFoundException("التركيب غير موجود"));

        private static string UnitName(string? branch, string? department) =>
            string.IsNullOrEmpty(branch) ? "—" : string.IsNullOrEmpty(department) ? branch : $"{branch} / {department}";
    }
}
