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
            if (entity.Project != null && entity.Project.Status != StudyProjectStatus.Cancelled)
                throw new InvalidOperationException("لأمر البدء مشروع قائم — ألغِ المشروع أولاً");

            entity.Status = StudyDocumentStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.StartOrder, entity.Id, StudyLogAction.Cancelled,
                $"ألغى أمر البدء {entity.OrderNumber}", request.Reason.Trim());
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
