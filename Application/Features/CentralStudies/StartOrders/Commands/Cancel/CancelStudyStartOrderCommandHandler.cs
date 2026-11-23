using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Commands.Cancel
{
    public class CancelStudyStartOrderCommandHandler : IRequestHandler<CancelStudyStartOrderCommand, StudyStartOrderDto>
    {
        private readonly IStudyStartOrderService _service;
        private readonly StudyKit _kit;
        private readonly StudyStartOrderSupport _support;

        public CancelStudyStartOrderCommandHandler(IStudyStartOrderService service, StudyKit kit, StudyStartOrderSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyStartOrderDto> Handle(CancelStudyStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled) throw new InvalidOperationException("أمر البدء ملغى مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft) throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");
            var activity = StudyKit.CreateActivity(viewer, entity.Response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Cancelled,
                $"ألغى أمر البدء {entity.OrderNumber}", request.Reason.Trim());
            await _service.CancelAsync(entity, activity, ct);
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
