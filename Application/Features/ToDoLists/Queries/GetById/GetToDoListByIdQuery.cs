using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Queries.GetById
{
    public record GetToDoListByIdQuery(int Id) : IRequest<ToDoListResponseDto>;
}
