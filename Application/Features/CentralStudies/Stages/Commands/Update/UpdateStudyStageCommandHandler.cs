using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Update
{
    public class UpdateStudyStageCommandHandler : IRequestHandler<UpdateStudyStageCommand, ProjectStageDto>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public UpdateStudyStageCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<ProjectStageDto> Handle(UpdateStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await _support.LoadStageAsync(request.Id, viewer);
            StudyStageSupport.EnsureOpen(stage.Project);
            await _support.EnsureResponsibleAsync(request.Dto.ResponsibleDepartmentId, stage.Project.BranchId);

            stage.Weight = request.Dto.Weight;
            stage.PlannedStartDate = request.Dto.PlannedStartDate?.Date;
            stage.PlannedEndDate = request.Dto.PlannedEndDate?.Date;
            stage.ResponsibleDepartmentId = request.Dto.ResponsibleDepartmentId;
            if (stage.Status != StudyStageStatus.Closed)
            {
                stage.ActualStartDate = request.Dto.ActualStartDate?.Date ?? stage.ActualStartDate;
                if (request.Dto.Status is int status)
                {
                    stage.Status = (StudyStageStatus)status;
                    if (stage.Status == StudyStageStatus.InProgress) stage.ActualStartDate ??= DateTime.Today;
                }
            }
            await _service.UpdateStageAsync(stage, request.Dto.RowVersion);
            await _support.LogAsync(stage.ProjectId, StudyEntityType.Stage, stage.Id, StudyLogAction.Edited, $"عدّل المرحلة «{stage.StageType.Name}»");
            return await _support.StageDetailAsync(stage.Id, viewer);
        }
    }
}
