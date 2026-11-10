using Application.DTOs.Response;
using Application.Features.CentralStudies.Stages;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Queries.GetMine
{
    public class GetMyStudyTasksQueryHandler : IRequestHandler<GetMyStudyTasksQuery, PagedResultDto<StageTaskDto>>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public GetMyStudyTasksQueryHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<PagedResultDto<StageTaskDto>> Handle(GetMyStudyTasksQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Page, request.PageSize);
            var (items, total) = await _service.GetAssignedAsync(viewer.Id, viewer.User.OfficeId, viewer.User.DepartmentId,
                request.OpenOnly, page, size);
            return new PagedResultDto<StageTaskDto>
            {
                Items = items.Select(t => _support.TaskDto(t, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }
    }
}
