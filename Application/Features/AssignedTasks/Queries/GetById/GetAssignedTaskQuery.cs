using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetById
{
    public record GetAssignedTaskQuery(int Id) : IRequest<AssignedTaskDetailDto>;
}
