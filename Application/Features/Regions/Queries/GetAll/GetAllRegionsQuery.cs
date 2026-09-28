using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Regions.Queries.GetAll
{
    public record GetAllRegionsQuery : IRequest<List<RegionResponseDto>>;
}
