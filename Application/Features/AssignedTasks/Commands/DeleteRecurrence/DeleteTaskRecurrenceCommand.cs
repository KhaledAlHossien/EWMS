using MediatR;

namespace Application.Features.AssignedTasks.Commands.DeleteRecurrence
{
    public record DeleteTaskRecurrenceCommand(int Id) : IRequest<Unit>;
}
