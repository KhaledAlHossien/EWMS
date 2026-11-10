using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Close
{
    public class CloseStudyStageCommandHandler : IRequestHandler<CloseStudyStageCommand, ProjectStageDto>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public CloseStudyStageCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<ProjectStageDto> Handle(CloseStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await _support.LoadStageAsync(request.Id, viewer);
            StudyStageSupport.EnsureOpen(stage.Project);
            StudyStageSupport.EnsureStageOpen(stage);
            var stages = await _service.GetStagesAsync(stage.ProjectId);
            if (stages.Any(s => s.SortOrder < stage.SortOrder && s.Status != StudyStageStatus.Closed))
                throw new InvalidOperationException("المراحل متتالية — أغلق المراحل السابقة أولاً");
            if (stage.Tasks.Any(t => t.Status != StudyTaskStatus.Done))
                throw new InvalidOperationException("في المرحلة مهام غير منجزة");

            stage.Status = StudyStageStatus.Closed;
            stage.ClosedByUserId = viewer.Id;
            stage.ClosedAt = DateTime.Now;
            stage.ActualStartDate ??= stage.Tasks.Min(t => t.ActualStartDate) ?? DateTime.Today;
            stage.ActualEndDate ??= DateTime.Today;
            await _service.UpdateStageAsync(stage);
            await _support.LogAsync(stage.ProjectId, StudyEntityType.Stage, stage.Id, StudyLogAction.Closed, $"أغلق المرحلة «{stage.StageType.Name}»");
            return await _support.StageDetailAsync(stage.Id, viewer);
        }
    }
}
