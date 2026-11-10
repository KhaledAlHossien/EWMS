using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Create
{
    public class CreateStudyStageCommandHandler : IRequestHandler<CreateStudyStageCommand, ProjectStageDto>
    {
        private readonly IStudyStageService _service;
        private readonly IStudyLookupService _lookups;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public CreateStudyStageCommandHandler(IStudyStageService service, IStudyLookupService lookups, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _lookups = lookups;
            _kit = kit;
            _support = support;
        }

        public async Task<ProjectStageDto> Handle(CreateStudyStageCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _support.LoadProjectAsync(request.Dto.ProjectId, viewer);
            StudyStageSupport.EnsureOpen(project);
            var type = await _lookups.GetStageTypeAsync(request.Dto.StageTypeId);
            if (type is not { IsActive: true }) throw new KeyNotFoundException("نوع المرحلة غير موجود أو غير فعّال");
            if (project.Stages.Any(s => s.StageTypeId == type.Id))
                throw new InvalidOperationException("نوع المرحلة موجود في المشروع مسبقاً");
            await _support.EnsureResponsibleAsync(request.Dto.ResponsibleDepartmentId, project.BranchId);

            var stage = new ProjectStage
            {
                ProjectId = project.Id,
                StageTypeId = type.Id,
                SortOrder = project.Stages.Count == 0 ? 1 : project.Stages.Max(s => s.SortOrder) + 1,
                Weight = request.Dto.Weight,
                PlannedStartDate = request.Dto.PlannedStartDate?.Date,
                PlannedEndDate = request.Dto.PlannedEndDate?.Date,
                ActualStartDate = request.Dto.ActualStartDate?.Date,
                ResponsibleDepartmentId = request.Dto.ResponsibleDepartmentId,
                Status = (StudyStageStatus?)request.Dto.Status ?? StudyStageStatus.NotStarted
            };
            await _service.AddStageAsync(stage);
            await _support.LogAsync(project.Id, StudyEntityType.Stage, stage.Id, StudyLogAction.Created, $"أضاف المرحلة «{type.Name}» إلى المشروع {project.Code}");
            return await _support.StageDetailAsync(stage.Id);
        }
    }
}
