using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Commands.Delete
{
    public class DeleteInstallationCommandHandler : IRequestHandler<DeleteInstallationCommand, Unit>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly InstallationSupport _support;

        public DeleteInstallationCommandHandler(IDeviceSiteService installations, IDeviceInventoryLogService log, ICurrentUserService currentUser, IUserService users, IUserPermissionService permissions, InstallationSupport support)
        {
            _installations = installations;
            _log = log;
            _currentUser = currentUser;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteInstallationCommand request, CancellationToken ct)
        {
            var installation = await _support.LoadInScopeAsync(request.Id, await CurrentAsync());
            var title = Title(installation, installation.Device?.Name ?? "", installation.Site?.Name ?? "");
            await _installations.DeleteAsync(installation);
            await _log.AddAsync(Log(DeviceInventoryEntity.Installation, request.Id, DeviceInventoryAction.Deleted, _currentUser.UserId, title,
                installation.SN.Length > 0 ? $"الرقم التسلسلي: {installation.SN}" : ""));
            return Unit.Value;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
