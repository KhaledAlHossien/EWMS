using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Sites.Queries.GetAll
{
    public class GetAllSitesQueryHandler : IRequestHandler<GetAllSitesQuery, List<SiteResponseDto>>
    {
        private readonly ISiteService _sites;
        private readonly SiteSupport _support;

        public GetAllSitesQueryHandler(ISiteService sites, SiteSupport support)
        {
            _sites = sites;
            _support = support;
        }

        public async Task<List<SiteResponseDto>> Handle(GetAllSitesQuery request, CancellationToken ct) =>
            (await _sites.GetAllWithCountsAsync(await _support.ScopeAsync())).Select(ToDto).ToList();
    }
}
