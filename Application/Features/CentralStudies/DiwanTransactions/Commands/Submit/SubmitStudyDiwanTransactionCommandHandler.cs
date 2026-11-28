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
            if (entity.Stage == StudyDiwanStage.ReadyToResend)
                throw new InvalidOperationException("سجّل الصادر الجديد ورد الديوان المركزي عليه قبل الإرسال إلى رئيس الفرع");
            if (!StudyDiwanTransactionSupport.CanBeSubmitted(entity.Stage))
                throw new InvalidOperationException($"المعاملة ليست لدى الديوان الآن ({StudyRules.Name(entity.Stage)})");
            StudyDiwanTransactionSupport.EnsureResponseApproved(entity);
            if (entity.ReplyNumber == null || entity.ReplyDate == null)
                throw new InvalidOperationException("سجّل رقم الرد المركزي وتاريخه قبل الإرسال إلى رئيس الفرع");

            // جولة بعد رفض مركزي: كتاب رد جديد برقم غير رقم الجولة السابقة، وصورته مرفوعة بعد إحالتها
            var last = StudyRules.LastRound(entity);
            if (last != null)
            {
                if (string.Equals(entity.ReplyNumber, last.ReplyNumber, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"سجّل رد الديوان المركزي على الصادر الجديد: الرقم {last.ReplyNumber} هو رد الجولة {last.RoundNumber}");
                if (await _attachments.CountForOwnerSinceAsync(StudyEntityType.DiwanTransaction, entity.Id, last.ReferredAt) == 0)
                    throw new InvalidOperationException("أرفق صورة كتاب الرد المركزي الجديد قبل الإرسال إلى رئيس الفرع");
            }
            else if (await _attachments.CountForOwnerAsync(StudyEntityType.DiwanTransaction, entity.Id) == 0)
                throw new InvalidOperationException("أرفق صورة كتاب الرد المركزي قبل الإرسال إلى رئيس الفرع");

            entity.CentralDecision = null;
            StudyDiwanTransactionSupport.MoveTo(entity, StudyDiwanStage.AtBranchHead);
            await _kit.ResetApprovalsAsync(StudyEntityType.DiwanTransaction, entity.Id);
            await _service.UpdateAsync(entity);

            await _kit.LogAsync(entity.Response.Assignment.RequestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Submitted,
                $"أرسل معاملة الديوان {entity.TransactionNumber} إلى رئيس الفرع",
                $"الصادر {entity.DispatchNumber}، الرد المركزي {entity.ReplyNumber}" + (entity.RoundNumber > 1 ? $" (الجولة {entity.RoundNumber})" : ""));
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(entity));
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
