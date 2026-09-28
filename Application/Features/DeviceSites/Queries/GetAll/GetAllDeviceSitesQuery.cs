using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceSites.Queries.GetAll
{
    public record GetAllDeviceSitesQuery(int? SiteId = null, int? DeviceId = null)
        : IRequest<List<DeviceSiteResponseDto>>;
}
