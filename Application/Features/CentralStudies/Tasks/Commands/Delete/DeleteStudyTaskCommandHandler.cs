using Application.Features.CentralStudies.Stages;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Commands.Delete
{
    public class DeleteStudyTaskCommandHandler : IRequestHandler<DeleteStudyTaskCommand, Unit>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public DeleteStudyTaskCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyTaskCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var task = await _service.GetTaskAsync(request.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            StudyRules.EnsureInScope(viewer, task.Stage.Project.BranchId);
            StudyRules.EnsureCanManageStageTasks(viewer, task.Stage);
            StudyStageSupport.EnsureOpen(task.Stage.Project);
            StudyStageSupport.EnsureStageOpen(task.Stage);
            var (projectId, title) = (task.Stage.ProjectId, task.Title);
            await _service.DeleteTaskAsync(task);
            await _support.LogAsync(projectId, StudyEntityType.Task, request.Id, StudyLogAction.Deleted, $"حذف المهمة «{title}»");
            return Unit.Value;
        }
    }
}
