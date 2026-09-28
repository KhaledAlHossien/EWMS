using Application.Interfaces;
using MediatR;

namespace Application.Features.WorkTasks.Commands.Delete
{
    public class DeleteWorkTaskCommandHandler : IRequestHandler<DeleteWorkTaskCommand, Unit>
    {
        private readonly IWorkTaskService _workTaskService;

        public DeleteWorkTaskCommandHandler(IWorkTaskService workTaskService)
        {
            _workTaskService = workTaskService;
        }

        public async Task<Unit> Handle(DeleteWorkTaskCommand request, CancellationToken cancellationToken)
        {
            var task = await _workTaskService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المهمة غير موجودة");

            // الإسنادات تُحذف معها (Cascade) — لا توجد بيانات أخرى مرتبطة بالمهمة حالياً
            await _workTaskService.DeleteAsync(task);
            return Unit.Value;
        }
    }
}
