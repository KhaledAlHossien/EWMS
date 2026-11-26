using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Sites.Commands.Create
{
    public record CreateSiteCommand(SiteRequestDto Dto) : IRequest<SiteResponseDto>;
}
