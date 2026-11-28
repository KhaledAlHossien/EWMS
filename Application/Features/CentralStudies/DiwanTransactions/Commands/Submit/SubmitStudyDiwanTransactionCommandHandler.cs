using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Submit
{
    public class SubmitStudyDiwanTransactionCommandHandler : IRequestHandler<SubmitStudyDiwanTransactionCommand, StudyDiwanTransactionDto>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly IStudyAttachmentService _attachments;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public SubmitStudyDiwanTransactionCommandHandler(IStudyDiwanTransactionService service, IStudyAttachmentService attachments,
            StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _attachments = attachments;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDiwanTransactionDto> Handle(SubmitStudyDiwanTransactionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            StudyDiwanTransactionSupport.EnsureResponseApproved(entity);
            if (entity.ReplyNumber == null || entity.ReplyDate == null || entity.CentralDecision == null)
                throw new InvalidOperationException("سجّل رقم الرد المركزي وتاريخه وقراره قبل الإرسال للاعتماد");
            if (await _attachments.CountForOwnerAsync(StudyEntityType.DiwanTransaction, entity.Id) == 0)
                throw new InvalidOperationException("أرفق صورة كتاب الرد المركزي قبل الإرسال للاعتماد");

            entity.Status = StudyDocumentStatus.PendingApproval;
            entity.UpdatedAt = DateTime.Now;
            await _kit.ResetApprovalsAsync(StudyEntityType.DiwanTransaction, entity.Id);
            await _service.UpdateAsync(entity);

            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Submitted,
                $"أرسل معاملة الديوان {entity.TransactionNumber} لاعتماد رئيس الفرع",
                $"الرد المركزي {entity.ReplyNumber}: {StudyRules.Name(entity.CentralDecision.Value)}");
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity));
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
