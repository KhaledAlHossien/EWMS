using MediatR;

namespace Application.Features.ToDoLists.Commands.Delete
{
    public record DeleteToDoListCommand(int Id) : IRequest<Unit>;
}
