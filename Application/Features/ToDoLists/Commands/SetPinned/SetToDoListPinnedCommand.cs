using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Commands.SetPinned
{
    public record SetToDoListPinnedCommand(int ListId, bool Value) : IRequest<ToDoListResponseDto>;
}
