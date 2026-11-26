using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.SetRecurrenceActive
{
    public class SetTaskRecurrenceActiveCommandHandler : IRequestHandler<SetTaskRecurrenceActiveCommand, TaskRecurrenceDto>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly AssignedTaskCreator _creator;
        private readonly TaskRecurrencesSupport _support;

        public SetTaskRecurrenceActiveCommandHandler(IAssignedTaskPlanningService planning, AssignedTaskCreator creator, TaskRecurrencesSupport support)
        {
            _planning = planning;
            _creator = creator;
            _support = support;
        }

        public async Task<TaskRecurrenceDto> Handle(SetTaskRecurrenceActiveCommand request, CancellationToken ct)
        {
            var viewer = await _support.AssignerAsync();
            var r = await _support.OwnedAsync(request.Id, viewer);
            if (request.IsActive)
            {
                await _creator.EnsureTargetAllowedAsync(viewer, r.TargetType, r.TargetId);
                var next = TaskPlanningRules.NextRun(r.Frequency, r.DayOfWeek, r.DayOfMonth, DateTime.Today);
                if (r.EndDate != null && next > r.EndDate) throw new InvalidOperationException("انتهت مدة هذه المهمة الدورية — عدّل تاريخ الانتهاء");
                r.NextRunDate = next;
                r.LastError = null;
            }
            r.IsActive = request.IsActive;
            await _planning.SaveChangesAsync();
            return TaskPlanningRules.ToDto(r, await _support.TargetNameAsync(r.TargetType, r.TargetId));
        }
    }
}
