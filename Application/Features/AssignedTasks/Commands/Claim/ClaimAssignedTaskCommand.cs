using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.Claim
{
    public record ClaimAssignedTaskCommand(int TaskId, bool Claim) : IRequest<AssignedTaskDetailDto>;
}
