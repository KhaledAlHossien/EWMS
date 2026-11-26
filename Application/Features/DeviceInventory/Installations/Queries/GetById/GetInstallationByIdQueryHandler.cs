using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Queries.GetById
{
    public class GetInstallationByIdQueryHandler : IRequestHandler<GetInstallationByIdQuery, DeviceSiteResponseDto>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly InstallationSupport _support;

        public GetInstallationByIdQueryHandler(IDeviceSiteService installations, IUserService users, IUserPermissionService permissions, InstallationSupport support)
        {
            _installations = installations;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<DeviceSiteResponseDto> Handle(GetInstallationByIdQuery request, CancellationToken ct)
        {
            await _support.LoadInScopeAsync(request.Id, await CurrentAsync());
            return await DtoAsync(request.Id);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<DeviceSiteResponseDto> DtoAsync(int id) =>
            ToDto(await _installations.GetRowAsync(id) ?? throw new KeyNotFoundException("التركيب غير موجود"));
    }
}
