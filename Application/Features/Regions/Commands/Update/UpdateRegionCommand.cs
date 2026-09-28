using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Regions.Commands.Update
{
    public record UpdateRegionCommand(int Id, UpdateRegionRequestDto RegionDto)
        : IRequest<RegionResponseDto>;
}
