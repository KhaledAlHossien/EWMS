using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Return
{
    public class ReturnStudyDiwanTransactionCommandHandler : IRequestHandler<ReturnStudyDiwanTransactionCommand, StudyDiwanTransactionDto>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public ReturnStudyDiwanTransactionCommandHandler(IStudyDiwanTransactionService service, StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDiwanTransactionDto> Handle(ReturnStudyDiwanTransactionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyDiwanTransactionSupport.EnsureStage(entity, StudyDiwanStage.AtBranchHead, "تُعاد المعاملة إلى الديوان وهي بانتظار رئيس الفرع");

            var reason = request.Dto.Reason.Trim();
            await _support.SignBranchHeadAsync(entity, viewer, StudyDecision.Rejected, reason);
            StudyDiwanTransactionSupport.MoveTo(entity, StudyDiwanStage.ReturnedToDiwan);
            await _service.UpdateAsync(entity, request.Dto.RowVersion);

            var owner = ApprovalOwner.Of(entity);
            await _kit.LogAsync(owner.RequestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Rejected,
                $"أعاد معاملة الديوان {entity.TransactionNumber} إلى الديوان للتصحيح", reason);
            await _kit.NotifyHoldersAsync(StudyPermissions.EditDiwan, owner.BranchId, null,
                "أُعيدت معاملة الديوان للتصحيح", $"{entity.TransactionNumber} — {reason}",
                StudyEntityType.DiwanTransaction, entity.Id, [entity.CreatedByUserId]);
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
