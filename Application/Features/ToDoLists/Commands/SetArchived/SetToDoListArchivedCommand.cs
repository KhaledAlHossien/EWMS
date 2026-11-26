using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Commands.SetArchived
{
    public record SetToDoListArchivedCommand(int ListId, bool Value) : IRequest<ToDoListResponseDto>;
}
