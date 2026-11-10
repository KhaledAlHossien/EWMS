using Application.DTOs.Response;
using Application.Features.CentralStudies.Stages;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Commands.UpdateProgress
{
    public class UpdateStudyTaskProgressCommandHandler : IRequestHandler<UpdateStudyTaskProgressCommand, StageTaskDto>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public UpdateStudyTaskProgressCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StageTaskDto> Handle(UpdateStudyTaskProgressCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var task = await _service.GetTaskAsync(request.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            if (!StudyStageSupport.IsTarget(viewer, task)) throw new UnauthorizedAccessException("يحدّث الإنجاز المكلَّف بالمهمة فقط");
            StudyStageSupport.EnsureOpen(task.Stage.Project);
            StudyStageSupport.EnsureStageOpen(task.Stage);

            var status = (StudyTaskStatus)request.Dto.Status;
            var progress = Math.Round(request.Dto.Progress, 1);
            if (status == StudyTaskStatus.Done) progress = 100;
            else if (progress >= 100) status = StudyTaskStatus.Done;
            else if (progress > 0 && status == StudyTaskStatus.NotStarted) status = StudyTaskStatus.InProgress;

            task.Progress = progress;
            task.Status = status;
            if (status != StudyTaskStatus.NotStarted) task.ActualStartDate ??= DateTime.Today;
            task.ActualEndDate = status == StudyTaskStatus.Done ? task.ActualEndDate ?? DateTime.Today : null;
            task.UpdatedAt = DateTime.Now;

            // أول عمل على مرحلة لم تبدأ يجعلها جارية
            if (status != StudyTaskStatus.NotStarted && task.Stage.Status == StudyStageStatus.NotStarted)
            {
                task.Stage.Status = StudyStageStatus.InProgress;
                task.Stage.ActualStartDate ??= DateTime.Today;
            }
            await _service.UpdateTaskAsync(task);
            await _support.LogAsync(task.Stage.ProjectId, StudyEntityType.Task, task.Id, StudyLogAction.Edited,
                $"حدّث إنجاز المهمة «{task.Title}»", $"{StudyRules.Name(status)} — {progress}%");
            return await _support.TaskDetailAsync(task.Id, viewer);
        }
    }
}
