using Application.DTOs.Response;
using Application.Features.CentralStudies.Stages;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Commands.Update
{
    public class UpdateStudyTaskCommandHandler : IRequestHandler<UpdateStudyTaskCommand, StageTaskDto>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public UpdateStudyTaskCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StageTaskDto> Handle(UpdateStudyTaskCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var task = await _service.GetTaskAsync(request.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            StudyRules.EnsureInScope(viewer, task.Stage.Project.BranchId);
            StudyStageSupport.EnsureOpen(task.Stage.Project);
            StudyStageSupport.EnsureStageOpen(task.Stage);
            await _support.ValidateTargetAsync(request.Dto, task.Stage.ProjectId);

            var oldTarget = (task.TargetType, task.DepartmentId, task.OfficeId, task.AssigneeUserId);
            task.Title = request.Dto.Title.Trim();
            task.Description = StudyRules.Clean(request.Dto.Description);
            task.PlannedStartDate = request.Dto.PlannedStartDate?.Date;
            task.PlannedEndDate = request.Dto.PlannedEndDate?.Date;
            task.Weight = request.Dto.Weight;
            StudyStageSupport.ApplyTarget(request.Dto, task);
            task.UpdatedAt = DateTime.Now;
            await _service.UpdateTaskAsync(task, request.Dto.RowVersion);

            await _support.LogAsync(task.Stage.ProjectId, StudyEntityType.Task, task.Id, StudyLogAction.Edited, $"عدّل المهمة «{task.Title}»");
            if (oldTarget != (task.TargetType, task.DepartmentId, task.OfficeId, task.AssigneeUserId))
                await _kit.NotifyTaskAssignedAsync(task, task.Stage.Project.Code);
            return await _support.TaskDetailAsync(task.Id, viewer);
        }
    }
}
