using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.Update
{
    public record UpdateToDoItemCommand(int ItemId, UpdateToDoItemRequestDto Dto) : IRequest<ToDoListResponseDto>;
}
