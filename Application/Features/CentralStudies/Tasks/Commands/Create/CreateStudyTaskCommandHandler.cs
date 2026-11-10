using Application.DTOs.Response;
using Application.Features.CentralStudies.Stages;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Commands.Create
{
    public class CreateStudyTaskCommandHandler : IRequestHandler<CreateStudyTaskCommand, StageTaskDto>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public CreateStudyTaskCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StageTaskDto> Handle(CreateStudyTaskCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var stage = await _support.LoadStageAsync(request.Dto.StageId, viewer);
            StudyStageSupport.EnsureOpen(stage.Project);
            StudyStageSupport.EnsureStageOpen(stage);
            await _support.ValidateTargetAsync(request.Dto, stage.ProjectId);

            var task = new StageTask
            {
                StageId = stage.Id,
                AssignedByUserId = viewer.Id,
                Title = request.Dto.Title.Trim(),
                Description = StudyRules.Clean(request.Dto.Description),
                PlannedStartDate = request.Dto.PlannedStartDate?.Date,
                PlannedEndDate = request.Dto.PlannedEndDate?.Date,
                Weight = request.Dto.Weight,
                Progress = 0,
                Status = StudyTaskStatus.NotStarted,
                CreatedAt = DateTime.Now
            };
            StudyStageSupport.ApplyTarget(request.Dto, task);
            await _service.AddTaskAsync(task);
            await _support.LogAsync(stage.ProjectId, StudyEntityType.Task, task.Id, StudyLogAction.Created,
                $"أضاف المهمة «{task.Title}» في المرحلة «{stage.StageType.Name}»");
            await _kit.NotifyTaskAssignedAsync(task, stage.Project.Code);
            return await _support.TaskDetailAsync(task.Id, viewer);
        }
    }
}
