using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetRecurrences
{
    public record GetTaskRecurrencesQuery : IRequest<List<TaskRecurrenceDto>>;
}
