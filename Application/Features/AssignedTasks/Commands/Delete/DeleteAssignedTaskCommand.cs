using MediatR;

namespace Application.Features.AssignedTasks.Commands.Delete
{
    public record DeleteAssignedTaskCommand(int Id) : IRequest<Unit>;
}
