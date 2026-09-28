using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Devices.Queries.GetBySite
{
    public record GetDevicesBySiteQuery(int SiteId) : IRequest<List<DeviceResponseDto>>;
}
