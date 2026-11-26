using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Queries.RevealPassword
{
    public class RevealDevicePasswordQueryHandler : IRequestHandler<RevealDevicePasswordQuery, DevicePasswordDto>
    {
        private readonly IDevicePasswordProtector _protector;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly InstallationSupport _support;

        public RevealDevicePasswordQueryHandler(IDevicePasswordProtector protector, IDeviceInventoryLogService log, ICurrentUserService currentUser, IUserService users, IUserPermissionService permissions, InstallationSupport support)
        {
            _protector = protector;
            _log = log;
            _currentUser = currentUser;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<DevicePasswordDto> Handle(RevealDevicePasswordQuery request, CancellationToken ct)
        {
            var installation = await _support.LoadInScopeAsync(request.Id, await CurrentAsync());
            if (string.IsNullOrEmpty(installation.Pass))
                throw new InvalidOperationException("لا توجد كلمة سر محفوظة لهذا التركيب");

            var password = _protector.Unprotect(installation.Pass);
            await _log.AddAsync(Log(DeviceInventoryEntity.Installation, installation.Id,
                request.Copy ? DeviceInventoryAction.PasswordCopied : DeviceInventoryAction.PasswordRevealed, _currentUser.UserId,
                Title(installation, installation.Device?.Name ?? "", installation.Site?.Name ?? "")));
            return new DevicePasswordDto { Password = password };
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
