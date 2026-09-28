using MediatR;

namespace Application.Features.WorkTasks.Commands.Delete
{
    public record DeleteWorkTaskCommand(int Id) : IRequest<Unit>;
}
