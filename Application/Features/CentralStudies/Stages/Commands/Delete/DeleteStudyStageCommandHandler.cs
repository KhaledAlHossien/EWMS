using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Delete
{
    public class DeleteStudyStageCommandHandler : IRequestHandler<DeleteStudyStageCommand, Unit>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public DeleteStudyStageCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await _support.LoadStageAsync(request.Id, viewer);
            StudyStageSupport.EnsureOpen(stage.Project);
            if (stage.StageType.LinkedDocument != StudyLinkedDocument.None)
                throw new InvalidOperationException("مرحلة المستند (الطلب/التكليف/الرد/أمر البدء) لا تُحذف");
            if (stage.Tasks.Count > 0) throw new InvalidOperationException("للمرحلة مهام — احذفها أولاً");
            if (stage.Status == StudyStageStatus.Closed) throw new InvalidOperationException("المرحلة مغلقة — لا تُحذف");

            var (projectId, name) = (stage.ProjectId, stage.StageType.Name);
            await _service.DeleteStageAsync(stage);
            await _support.LogAsync(projectId, StudyEntityType.Stage, request.Id, StudyLogAction.Deleted, $"حذف المرحلة «{name}»");
            return Unit.Value;
        }
    }
}
