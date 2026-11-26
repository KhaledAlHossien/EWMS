using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Sites.Commands.Update
{
    public record UpdateSiteCommand(int Id, SiteRequestDto Dto) : IRequest<SiteResponseDto>;
}
