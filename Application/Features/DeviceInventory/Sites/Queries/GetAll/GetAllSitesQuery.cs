using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Sites.Queries.GetAll
{
    public record GetAllSitesQuery : IRequest<List<SiteResponseDto>>;
}
