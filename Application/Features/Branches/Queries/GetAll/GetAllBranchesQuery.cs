using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Branches.Queries.GetAll
{
    public record GetAllBranchesQuery : IRequest<List<BranchResponseDto>>;
}
