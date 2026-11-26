using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetRecurrences
{
    public class GetTaskRecurrencesQueryHandler : IRequestHandler<GetTaskRecurrencesQuery, List<TaskRecurrenceDto>>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly TaskRecurrencesSupport _support;

        public GetTaskRecurrencesQueryHandler(IAssignedTaskPlanningService planning, TaskRecurrencesSupport support)
        {
            _planning = planning;
            _support = support;
        }

        public async Task<List<TaskRecurrenceDto>> Handle(GetTaskRecurrencesQuery request, CancellationToken ct)
        {
            var viewer = await _support.AssignerAsync();
            var list = new List<TaskRecurrenceDto>();
            foreach (var r in await _planning.GetRecurrencesAsync(viewer.Id))
                list.Add(TaskPlanningRules.ToDto(r, await _support.TargetNameAsync(r.TargetType, r.TargetId)));
            return list;
        }
    }
}
