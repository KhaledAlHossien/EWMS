using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Commands.Update
{
    public record UpdateToDoListCommand(int Id, ToDoListRequestDto Dto) : IRequest<ToDoListResponseDto>;
}
