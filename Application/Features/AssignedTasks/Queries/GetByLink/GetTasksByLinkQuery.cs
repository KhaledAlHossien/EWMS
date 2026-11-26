using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetByLink
{
    public record GetTasksByLinkQuery(string EntityType, int EntityId) : IRequest<List<AssignedTaskCardDto>>;
}
