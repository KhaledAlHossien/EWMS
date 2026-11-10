using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Reopen
{
    public class ReopenStudyStageCommandHandler : IRequestHandler<ReopenStudyStageCommand, ProjectStageDto>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public ReopenStudyStageCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<ProjectStageDto> Handle(ReopenStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await _support.LoadStageAsync(request.Id, viewer);
            StudyStageSupport.EnsureOpen(stage.Project);
            if (stage.Status != StudyStageStatus.Closed) throw new InvalidOperationException("المرحلة غير مغلقة");
            if (stage.StageType.LinkedDocument != StudyLinkedDocument.None)
                throw new InvalidOperationException("مرحلة المستند أُغلقت من المستند المعتمد — لا يُعاد فتحها");
            var stages = await _service.GetStagesAsync(stage.ProjectId);
            if (stages.Any(s => s.SortOrder > stage.SortOrder && s.Status == StudyStageStatus.Closed))
                throw new InvalidOperationException("أُغلقت مراحل بعدها — يُعاد فتح آخر مرحلة مغلقة فقط");

            stage.Status = StudyStageStatus.InProgress;
            stage.ClosedByUserId = null;
            stage.ClosedAt = null;
            stage.ActualEndDate = null;
            await _service.UpdateStageAsync(stage);
            await _support.LogAsync(stage.ProjectId, StudyEntityType.Stage, stage.Id, StudyLogAction.Reopened, $"أعاد فتح المرحلة «{stage.StageType.Name}»");
            return await _support.StageDetailAsync(stage.Id, viewer);
        }
    }
}
