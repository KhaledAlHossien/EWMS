using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.Reorder
{
    public record ReorderToDoItemsCommand(int ListId, List<int> ItemIds) : IRequest<ToDoListResponseDto>;
}
