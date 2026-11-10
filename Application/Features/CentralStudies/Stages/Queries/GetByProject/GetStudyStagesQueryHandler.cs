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
            await _support.LoadProjectAsync(request.ProjectId, viewer);
            return (await _service.GetStagesAsync(request.ProjectId)).Select(_support.StageDto).ToList();
        }
    }
}
