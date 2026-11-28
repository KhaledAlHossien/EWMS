using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Cancel
{
    public class CancelStudyDiwanTransactionCommandHandler : IRequestHandler<CancelStudyDiwanTransactionCommand, StudyDiwanTransactionDto>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public CancelStudyDiwanTransactionCommandHandler(IStudyDiwanTransactionService service, StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDiwanTransactionDto> Handle(CancelStudyDiwanTransactionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            if (entity.Status == StudyDocumentStatus.Cancelled) throw new InvalidOperationException("معاملة الديوان ملغاة مسبقاً");
            if (entity.Status == StudyDocumentStatus.Draft) throw new InvalidOperationException("المسودة تُحذف ولا تُلغى");
            if (entity.Response.StartOrder is { } order && order.Status != StudyDocumentStatus.Cancelled)
                throw new InvalidOperationException("للمعاملة أمر بدء قائم — ألغِ أمر البدء أولاً");

            entity.Status = StudyDocumentStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Cancelled,
                $"ألغى معاملة الديوان {entity.TransactionNumber}", request.Reason.Trim());
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
