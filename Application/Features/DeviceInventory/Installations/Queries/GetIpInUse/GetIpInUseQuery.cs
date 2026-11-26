using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Queries.GetIpInUse
{
    public record GetIpInUseQuery(int SiteId, string Ip, int? ExcludeId) : IRequest<List<IpInUseDto>>;
}
