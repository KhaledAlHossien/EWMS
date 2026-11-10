using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.ActivityLogs.Queries.GetByProject
{
    public class GetProjectActivityLogQueryHandler : IRequestHandler<GetProjectActivityLogQuery, PagedResultDto<StudyActivityLogDto>>
    {
        private readonly IStudyProjectService _projects;
        private readonly StudyKit _kit;
        private readonly StudyActivityLogSupport _support;

        public GetProjectActivityLogQueryHandler(IStudyProjectService projects, StudyKit kit, StudyActivityLogSupport support)
        {
            _projects = projects;
            _kit = kit;
            _support = support;
        }

        public async Task<PagedResultDto<StudyActivityLogDto>> Handle(GetProjectActivityLogQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _projects.GetByIdAsync(request.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            return await _support.PageAsync(project.StartOrder.Response.Assignment.RequestId, request.Page, request.PageSize);
        }
    }
}
