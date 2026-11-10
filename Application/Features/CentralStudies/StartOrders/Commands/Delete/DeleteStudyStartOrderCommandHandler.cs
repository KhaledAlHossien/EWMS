using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Delete
{
    public class DeleteStudyStartOrderCommandHandler : IRequestHandler<DeleteStudyStartOrderCommand, Unit>
    {
        private readonly IStudyStartOrderService _service;
        private readonly StudyKit _kit;
        private readonly StudyStartOrderSupport _support;

        public DeleteStudyStartOrderCommandHandler(IStudyStartOrderService service, StudyKit kit, StudyStartOrderSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.Project != null) throw new InvalidOperationException("لأمر البدء مشروع — لا يُحذف");
            var requestId = entity.Response.Assignment.RequestId;
            var number = entity.OrderNumber;
            await _service.DeleteAsync(entity);
            await _kit.LogAsync(requestId, StudyEntityType.StartOrder, request.Id, StudyLogAction.Deleted, $"حذف مسودة أمر البدء {number}");
            return Unit.Value;
        }
    }
}
