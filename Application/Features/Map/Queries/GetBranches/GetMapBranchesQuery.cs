using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Map.Queries.GetBranches
{
    public record GetMapBranchesQuery : IRequest<List<MapBranchOptionDto>>;
}
