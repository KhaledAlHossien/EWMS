using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Commands.Verify
{
    public class VerifyInstallationCommandHandler : IRequestHandler<VerifyInstallationCommand, DeviceSiteResponseDto>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly InstallationSupport _support;

        public VerifyInstallationCommandHandler(IDeviceSiteService installations, IDeviceInventoryLogService log, ICurrentUserService currentUser, IUserService users, IUserPermissionService permissions, InstallationSupport support)
        {
            _installations = installations;
            _log = log;
            _currentUser = currentUser;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<DeviceSiteResponseDto> Handle(VerifyInstallationCommand request, CancellationToken ct)
        {
            var installation = await _support.LoadInScopeAsync(request.Id, await CurrentAsync());
            installation.LastVerifiedAt = DateTime.Today;
            await _installations.UpdateAsync(installation, null);
            await _log.AddAsync(Log(DeviceInventoryEntity.Installation, installation.Id, DeviceInventoryAction.Verified, _currentUser.UserId,
                Title(installation, installation.Device?.Name ?? "", installation.Site?.Name ?? "")));
            return await DtoAsync(installation.Id);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<DeviceSiteResponseDto> DtoAsync(int id) =>
            ToDto(await _installations.GetRowAsync(id) ?? throw new KeyNotFoundException("التركيب غير موجود"));
    }
}
