using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Commands.Duplicate
{
    public record DuplicateToDoListCommand(int ListId) : IRequest<ToDoListResponseDto>;
}
