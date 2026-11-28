using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Reactivate
{
    public class ReactivateStudyProjectCommandHandler : IRequestHandler<ReactivateStudyProjectCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;
        private readonly INotificationPusher _pusher;

        public ReactivateStudyProjectCommandHandler(IStudyProjectService service, StudyKit kit, StudyProjectSupport support, INotificationPusher pusher)
        {
            _service = service;
            _kit = kit;
            _support = support;
            _pusher = pusher;
        }

        public async Task<StudyProjectDto> Handle(ReactivateStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            StudyRules.Require(viewer, StudyPermissions.CancelDocuments);
            var project = await _support.LoadAsync(request.Id, viewer);
            if (project.Status != StudyProjectStatus.Cancelled)
                throw new InvalidOperationException("إعادة التفعيل متاحة للمشاريع الملغاة فقط");

            var requestId = project.RequestId;
            var activity = StudyKit.CreateActivity(viewer, requestId, StudyEntityType.Project,
                project.Id, StudyLogAction.Reactivated, $"أعاد تفعيل المشروع {project.Code}");
            var notifications = await _kit.ProjectReactivationNotificationsAsync(project, requestId, ct);
            await _service.ReactivateAsync(project, activity, notifications, ct);
            await _pusher.PushAsync(notifications);
            return await _support.DetailAsync(project.Id, viewer);
        }
    }
}
