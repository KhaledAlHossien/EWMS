using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.ClearDone
{
    public record ClearDoneToDoItemsCommand(int ListId) : IRequest<ToDoListResponseDto>;
}
