using Application.DTOs.Response;
using Application.Features.CentralStudies.Stages;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Queries.GetByStage
{
    public class GetStudyTasksQueryHandler : IRequestHandler<GetStudyTasksQuery, List<StageTaskDto>>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public GetStudyTasksQueryHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<List<StageTaskDto>> Handle(GetStudyTasksQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await _support.LoadStageAsync(request.StageId, viewer);
            var scope = ProjectScope.For(viewer, stage.Project);
            await _support.EnsureStageVisibleAsync(scope, stage);
            return (await _service.GetTasksAsync(request.StageId, scope.TaskFilter())).Select(t => _support.TaskDto(t, viewer)).ToList();
        }
    }
}
