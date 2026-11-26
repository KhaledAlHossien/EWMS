using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Commands.Create
{
    public record CreateToDoListCommand(ToDoListRequestDto Dto) : IRequest<ToDoListResponseDto>;
}
