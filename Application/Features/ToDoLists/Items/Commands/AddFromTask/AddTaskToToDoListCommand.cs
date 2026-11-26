using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.AddFromTask
{
    public record AddTaskToToDoListCommand(int ListId, int TaskId) : IRequest<ToDoListResponseDto>;
}
