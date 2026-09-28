using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Regions.Commands.Create
{
    public record CreateRegionCommand(CreateRegionRequestDto RegionDto)
        : IRequest<RegionResponseDto>;
}
