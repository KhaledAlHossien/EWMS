using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetLinks
{
    public record GetTaskLinksQuery(int TaskId) : IRequest<List<TaskLinkDto>>;
}
