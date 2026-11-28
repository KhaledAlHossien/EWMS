using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Delete
{
    public class DeleteStudyDiwanTransactionCommandHandler : IRequestHandler<DeleteStudyDiwanTransactionCommand, Unit>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public DeleteStudyDiwanTransactionCommandHandler(IStudyDiwanTransactionService service, StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyDiwanTransactionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.Rounds.Count > 0)
                throw new InvalidOperationException("للمعاملة جولات سابقة مع الديوان المركزي — تُلغى ولا تُحذف");
            var requestId = entity.Response.Assignment.RequestId;
            var number = entity.TransactionNumber;
            await _service.DeleteAsync(entity);
            await _kit.LogAsync(requestId, StudyEntityType.DiwanTransaction, request.Id, StudyLogAction.Deleted, $"حذف مسودة معاملة الديوان {number}");
            return Unit.Value;
        }
    }
}
