using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Regions.Queries.GetSites
{
    public record GetSitesByRegionQuery(int RegionId) : IRequest<List<SiteResponseDto>>;
}
