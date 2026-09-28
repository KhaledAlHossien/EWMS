using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Sites.Commands.Create
{
    public record CreateSiteCommand(CreateSiteRequestDto SiteDto)
        : IRequest<SiteResponseDto>;
}
