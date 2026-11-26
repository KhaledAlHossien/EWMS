using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.SetRecurrenceActive
{
    public record SetTaskRecurrenceActiveCommand(int Id, bool IsActive) : IRequest<TaskRecurrenceDto>;
}
