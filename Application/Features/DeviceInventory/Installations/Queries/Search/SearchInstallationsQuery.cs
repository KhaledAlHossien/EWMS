using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Queries.Search
{
    public record SearchInstallationsQuery(InstallationFilterDto Filter) : IRequest<PagedResultDto<DeviceSiteResponseDto>>;
}
