using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Projects.Commands.Transfer
{
    public record TransferProjectCommand(int ProjectId, TransferProjectRequestDto TransferDto)
        : IRequest<ProjectTransferResponseDto>;
}
