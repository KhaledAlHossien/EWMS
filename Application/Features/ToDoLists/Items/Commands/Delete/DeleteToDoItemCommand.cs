using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.Delete
{
    public record DeleteToDoItemCommand(int ItemId) : IRequest<ToDoListResponseDto>;
}
