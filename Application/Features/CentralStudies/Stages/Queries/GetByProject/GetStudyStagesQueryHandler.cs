using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Queries.GetByProject
{
    public class GetStudyStagesQueryHandler : IRequestHandler<GetStudyStagesQuery, List<ProjectStageDto>>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public GetStudyStagesQueryHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<List<ProjectStageDto>> Handle(GetStudyStagesQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _support.LoadProjectAsync(request.ProjectId, viewer);
            var stages = await _support.VisibleStagesAsync(project, await _service.GetStagesAsync(request.ProjectId), viewer);
            return stages.Select(s => _support.StageDto(s, project, viewer)).ToList();
        }
    }
}
