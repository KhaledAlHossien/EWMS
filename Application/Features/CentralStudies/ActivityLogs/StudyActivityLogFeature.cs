using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.ActivityLogs
{
    // سجل الدراسة: كل ما حدث من الطلب حتى إغلاق المشروع (الأحدث أولاً). ViewStudyActivityLog — دراسات فرعه.
    // مرقَّم (page، pageSize ≤ 100): السجل يكبر مع الزمن ولا ينقص.
    public record GetStudyActivityLogQuery(int RequestId, int Page, int PageSize) : IRequest<PagedResultDto<StudyActivityLogDto>>;
    public record GetProjectActivityLogQuery(int ProjectId, int Page, int PageSize) : IRequest<PagedResultDto<StudyActivityLogDto>>;

    public class StudyActivityLogHandler :
        IRequestHandler<GetStudyActivityLogQuery, PagedResultDto<StudyActivityLogDto>>,
        IRequestHandler<GetProjectActivityLogQuery, PagedResultDto<StudyActivityLogDto>>
    {
        private readonly IStudyActivityLogService _service;
        private readonly IStudyRequestService _requests;
        private readonly IStudyProjectService _projects;
        private readonly StudyKit _kit;

        public StudyActivityLogHandler(IStudyActivityLogService service, IStudyRequestService requests,
            IStudyProjectService projects, StudyKit kit)
        {
            _service = service;
            _requests = requests;
            _projects = projects;
            _kit = kit;
        }

        // الافتراضي 50 حدثاً في الصفحة (الأقصى 100)
        private async Task<PagedResultDto<StudyActivityLogDto>> PageAsync(int requestId, int page, int pageSize)
        {
            var (p, size) = StudyRules.NormalizePaging(page, pageSize <= 0 ? 50 : pageSize);
            var (items, total) = await _service.GetForRequestAsync(requestId, p, size);
            return new PagedResultDto<StudyActivityLogDto> { Items = items, TotalCount = total, Page = p, PageSize = size };
        }

        public async Task<PagedResultDto<StudyActivityLogDto>> Handle(GetStudyActivityLogQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var studyRequest = await _requests.GetByIdAsync(request.RequestId) ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, studyRequest.BranchId);
            return await PageAsync(studyRequest.Id, request.Page, request.PageSize);
        }

        public async Task<PagedResultDto<StudyActivityLogDto>> Handle(GetProjectActivityLogQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _projects.GetByIdAsync(request.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            return await PageAsync(project.StartOrder.Response.Assignment.RequestId, request.Page, request.PageSize);
        }
    }
}
