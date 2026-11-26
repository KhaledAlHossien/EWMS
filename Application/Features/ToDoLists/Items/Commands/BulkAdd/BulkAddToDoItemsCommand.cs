using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.BulkAdd
{
    public record BulkAddToDoItemsCommand(int ListId, List<string> Titles) : IRequest<ToDoListResponseDto>;
}
