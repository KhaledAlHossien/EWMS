using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.DeleteRecurrence
{
    public class DeleteTaskRecurrenceCommandHandler : IRequestHandler<DeleteTaskRecurrenceCommand, Unit>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly TaskRecurrencesSupport _support;

        public DeleteTaskRecurrenceCommandHandler(IAssignedTaskPlanningService planning, TaskRecurrencesSupport support)
        {
            _planning = planning;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteTaskRecurrenceCommand request, CancellationToken ct)
        {
            var viewer = await _support.AssignerAsync();
            await _planning.DeleteRecurrenceAsync(await _support.OwnedAsync(request.Id, viewer));
            return Unit.Value;
        }
    }
}
