using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.ActivityLogs.Queries.GetByRequest
{
    public class GetStudyActivityLogQueryHandler : IRequestHandler<GetStudyActivityLogQuery, PagedResultDto<StudyActivityLogDto>>
    {
        private readonly IStudyRequestService _requests;
        private readonly IStudyProjectService _projects;
        private readonly StudyKit _kit;
        private readonly StudyActivityLogSupport _support;

        public GetStudyActivityLogQueryHandler(IStudyRequestService requests, IStudyProjectService projects, StudyKit kit, StudyActivityLogSupport support)
        {
            _requests = requests;
            _projects = projects;
            _kit = kit;
            _support = support;
        }

        public async Task<PagedResultDto<StudyActivityLogDto>> Handle(GetStudyActivityLogQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var studyRequest = await _requests.GetByIdAsync(request.RequestId) ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, studyRequest.BranchId);
            // دراسة لها مشروع: حدّ رؤية المشروع نفسه (قبل المشروع لا حدّ إضافياً)
            var project = await _projects.GetBriefByRequestAsync(studyRequest.Id);
            return project is var (projectId, managerId)
                ? await _support.PageAsync(studyRequest.Id, request.Page, request.PageSize, projectId, ProjectScope.For(viewer, managerId))
                : await _support.PageAsync(studyRequest.Id, request.Page, request.PageSize);
        }
    }
}
