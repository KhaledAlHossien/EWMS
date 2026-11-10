using Application.Features.CentralStudies.Stages;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Queries.GetByProject
{
    public class GetStudyProjectTasksQueryHandler : IRequestHandler<GetStudyProjectTasksQuery, StudyProjectTasksResult>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public GetStudyProjectTasksQueryHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        /// <summary>حدّ المشروع نفسه في StudyStages/ByProject، والأعلام نفسها في ByStage</summary>
        public async Task<StudyProjectTasksResult> Handle(GetStudyProjectTasksQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            await _support.LoadProjectAsync(request.ProjectId, viewer);
            var (items, total) = await _service.GetProjectTasksAsync(request.ProjectId, StudyRules.MaxProjectTasks);
            return new StudyProjectTasksResult(items.Select(t => _support.TaskDto(t, viewer)).ToList(), total);
        }
    }
}
