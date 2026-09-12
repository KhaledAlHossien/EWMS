using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Branches.Commands.Update
{
    public record UpdateBranchCommand(int Id, UpdateBranchRequestDto BranchDto) : IRequest<BranchResponseDto>;
}
