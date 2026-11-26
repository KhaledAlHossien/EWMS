using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Queries.GetIpInUse
{
    public class GetIpInUseQueryHandler : IRequestHandler<GetIpInUseQuery, List<IpInUseDto>>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public GetIpInUseQueryHandler(IDeviceSiteService installations, IUserService users, IUserPermissionService permissions)
        {
            _installations = installations;
            _users = users;
            _permissions = permissions;
        }

        public async Task<List<IpInUseDto>> Handle(GetIpInUseQuery request, CancellationToken ct)
        {
            if (request.SiteId <= 0 || !NetworkRules.IsIpv4(request.Ip)) return [];
            return (await _installations.IpInUseAsync(request.SiteId, request.Ip.Trim(), request.ExcludeId, ScopeFor(await CurrentAsync())))
                .Select(i => new IpInUseDto { Id = i.Id, DeviceName = i.Device?.Name ?? string.Empty, InstallLocation = i.InstallLocation })
                .ToList();
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
