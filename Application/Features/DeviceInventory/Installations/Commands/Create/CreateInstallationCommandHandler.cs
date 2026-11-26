using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Commands.Create
{
    public class CreateInstallationCommandHandler : IRequestHandler<CreateInstallationCommand, DeviceSiteResponseDto>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IDevicePasswordProtector _protector;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly InstallationSupport _support;

        public CreateInstallationCommandHandler(IDeviceSiteService installations, IDevicePasswordProtector protector, IDeviceInventoryLogService log, ICurrentUserService currentUser, IUserService users, IUserPermissionService permissions, InstallationSupport support)
        {
            _installations = installations;
            _protector = protector;
            _log = log;
            _currentUser = currentUser;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<DeviceSiteResponseDto> Handle(CreateInstallationCommand request, CancellationToken ct)
        {
            var (device, site) = await _support.ReferencesAsync(request.Dto.DeviceId, request.Dto.SiteId);
            var (branchId, departmentId) = await _support.UnitAsync(await CurrentAsync(), request.Dto.DepartmentId);

            // الجهاز نوع/موديل قابل للتكرار: يمكن تركيب نفس الجهاز أكثر من مرة في نفس الموقع — قرار المستخدم 2026-09-29
            var installation = new DeviceSite { DeviceId = device.Id, SiteId = site.Id, BranchId = branchId, DepartmentId = departmentId };
            InstallationSupport.Apply(request.Dto, installation);
            installation.Pass = _protector.Protect(request.Dto.Pass);
            await _installations.AddAsync(installation);

            await _log.AddAsync(Log(DeviceInventoryEntity.Installation, installation.Id, DeviceInventoryAction.Created, _currentUser.UserId,
                Title(installation, device.Name, site.Name), $"الحالة: {StatusAr(installation.Status)}" + (installation.SN.Length > 0 ? $"\nالرقم التسلسلي: {installation.SN}" : "")));
            return await DtoAsync(installation.Id);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<DeviceSiteResponseDto> DtoAsync(int id) =>
            ToDto(await _installations.GetRowAsync(id) ?? throw new KeyNotFoundException("التركيب غير موجود"));
    }
}
