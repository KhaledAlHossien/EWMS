using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Installations.Queries.Search
{
    public class SearchInstallationsQueryHandler : IRequestHandler<SearchInstallationsQuery, PagedResultDto<DeviceSiteResponseDto>>
    {
        private readonly IDeviceSiteService _installations;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public SearchInstallationsQueryHandler(IDeviceSiteService installations, IUserService users, IUserPermissionService permissions)
        {
            _installations = installations;
            _users = users;
            _permissions = permissions;
        }

        public async Task<PagedResultDto<DeviceSiteResponseDto>> Handle(SearchInstallationsQuery request, CancellationToken ct)
        {
            var (page, pageSize) = Paging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _installations.SearchAsync(request.Filter, ScopeFor(await CurrentAsync()), page, pageSize);
            return new PagedResultDto<DeviceSiteResponseDto> { Items = items.Select(ToDto).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
