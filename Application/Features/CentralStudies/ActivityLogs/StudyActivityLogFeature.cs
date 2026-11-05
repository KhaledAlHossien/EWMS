using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.ActivityLogs
{
    // سجل الدراسة: كل ما حدث من الطلب حتى إغلاق المشروع (الأحدث أولاً). ViewStudyActivityLog — دراسات فرعه.
    public record GetStudyActivityLogQuery(int RequestId) : IRequest<List<StudyActivityLogDto>>;
    public record GetProjectActivityLogQuery(int ProjectId) : IRequest<List<StudyActivityLogDto>>;

    public class StudyActivityLogHandler :
        IRequestHandler<GetStudyActivityLogQuery, List<StudyActivityLogDto>>,
        IRequestHandler<GetProjectActivityLogQuery, List<StudyActivityLogDto>>
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

        public async Task<List<StudyActivityLogDto>> Handle(GetStudyActivityLogQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var studyRequest = await _requests.GetByIdAsync(request.RequestId) ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, studyRequest.BranchId);
            return _kit.Mapper.Map<List<StudyActivityLogDto>>(await _service.GetForRequestAsync(studyRequest.Id));
        }

        public async Task<List<StudyActivityLogDto>> Handle(GetProjectActivityLogQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _projects.GetByIdAsync(request.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            return _kit.Mapper.Map<List<StudyActivityLogDto>>(await _service.GetForRequestAsync(project.StartOrder.Response.Assignment.RequestId));
        }
    }
}
