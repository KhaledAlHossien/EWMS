using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetTemplates
{
    public class GetTaskTemplatesQueryHandler : IRequestHandler<GetTaskTemplatesQuery, List<TaskTemplateDto>>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly TaskTemplatesSupport _support;

        public GetTaskTemplatesQueryHandler(IAssignedTaskPlanningService planning, TaskTemplatesSupport support)
        {
            _planning = planning;
            _support = support;
        }

        public async Task<List<TaskTemplateDto>> Handle(GetTaskTemplatesQuery request, CancellationToken ct)
        {
            var viewer = await _support.AssignerAsync();
            return (await _planning.GetTemplatesAsync(viewer.Id)).Select(TaskPlanningRules.ToDto).ToList();
        }
    }
}
