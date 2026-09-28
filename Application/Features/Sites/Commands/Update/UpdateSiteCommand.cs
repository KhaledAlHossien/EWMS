using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Sites.Commands.Update
{
    public record UpdateSiteCommand(int Id, UpdateSiteRequestDto SiteDto)
        : IRequest<SiteResponseDto>;
}
