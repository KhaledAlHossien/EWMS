using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Regions.Queries.GetById
{
    public record GetRegionByIdQuery(int Id) : IRequest<RegionResponseDto>;
}
