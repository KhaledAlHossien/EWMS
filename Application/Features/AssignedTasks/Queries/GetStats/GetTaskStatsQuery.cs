using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetStats
{
    public record GetTaskStatsQuery(DateTime? From, DateTime? To) : IRequest<TaskStatsDto>;
}
