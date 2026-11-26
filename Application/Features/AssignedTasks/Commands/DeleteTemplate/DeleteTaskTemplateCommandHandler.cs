using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.DeleteTemplate
{
    public class DeleteTaskTemplateCommandHandler : IRequestHandler<DeleteTaskTemplateCommand, Unit>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly TaskTemplatesSupport _support;

        public DeleteTaskTemplateCommandHandler(IAssignedTaskPlanningService planning, TaskTemplatesSupport support)
        {
            _planning = planning;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteTaskTemplateCommand request, CancellationToken ct)
        {
            var viewer = await _support.AssignerAsync();
            var template = await _planning.GetTemplateAsync(request.Id) ?? throw new KeyNotFoundException("القالب غير موجود");
            if (template.OwnerUserId != viewer.Id) throw new UnauthorizedAccessException("القالب لصاحبه فقط");
            if (await _planning.TemplateInUseAsync(template.Id))
                throw new InvalidOperationException("القالب مستعمل في مهمة دورية — احذف المهمة الدورية أولاً");
            await _planning.DeleteTemplateAsync(template);
            return Unit.Value;
        }
    }
}
