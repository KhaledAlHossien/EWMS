using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Branches.Queries.GetById
{
    public record GetBranchByIdQuery(int Id) : IRequest<BranchResponseDto>;
}
