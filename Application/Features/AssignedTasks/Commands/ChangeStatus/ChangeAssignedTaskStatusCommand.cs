using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.ChangeStatus
{
    public record ChangeAssignedTaskStatusCommand(int Id, int Status, string? Note = null, int? ExpectedStatus = null) : IRequest<AssignedTaskCardDto>;
}
