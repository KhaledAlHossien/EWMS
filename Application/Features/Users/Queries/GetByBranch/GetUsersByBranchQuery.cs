using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Queries.GetByBranch
{
    public record GetUsersByBranchQuery(int BranchId) : IRequest<List<UserResponseDto>>;
}
