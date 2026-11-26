using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.RunRecurrenceNow
{
    public class RunTaskRecurrenceNowCommandHandler : IRequestHandler<RunTaskRecurrenceNowCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly AssignedTaskCreator _creator;
        private readonly TaskRecurrencesSupport _support;

        public RunTaskRecurrenceNowCommandHandler(IAssignedTaskPlanningService planning, AssignedTaskCreator creator, TaskRecurrencesSupport support)
        {
            _planning = planning;
            _creator = creator;
            _support = support;
        }

        public async Task<AssignedTaskDetailDto> Handle(RunTaskRecurrenceNowCommand request, CancellationToken ct)
        {
            var viewer = await _support.AssignerAsync();
            var r = await _support.OwnedAsync(request.Id, viewer);
            var created = await _creator.CreateAsync(viewer, AssignedTaskRecurrenceRunner.SpecFor(r, DateTime.Today));
            r.LastRunAt = DateTime.UtcNow;
            r.LastTaskId = created.Id;
            r.LastError = null;
            await _planning.SaveChangesAsync();
            return AssignedTaskRules.ToDetail(created, viewer);
        }
    }
}
