using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Branches.Commands.Create
{
    public record CreateBranchCommand(CreateBranchRequestDto BranchDto) : IRequest<BranchResponseDto>;
}
