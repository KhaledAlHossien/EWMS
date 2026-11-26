using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Sites.Queries.GetById
{
    public record GetSiteByIdQuery(int Id) : IRequest<SiteResponseDto>;
}
