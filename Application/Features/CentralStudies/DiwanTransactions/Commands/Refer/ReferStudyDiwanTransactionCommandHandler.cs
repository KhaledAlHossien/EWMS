using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Refer
{
    public class ReferStudyDiwanTransactionCommandHandler : IRequestHandler<ReferStudyDiwanTransactionCommand, StudyDiwanTransactionDto>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public ReferStudyDiwanTransactionCommandHandler(IStudyDiwanTransactionService service, StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDiwanTransactionDto> Handle(ReferStudyDiwanTransactionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyDiwanTransactionSupport.EnsureStage(entity, StudyDiwanStage.AtBranchHead, "تُحال المعاملة وهي بانتظار رئيس الفرع");
            if (entity.ReplyNumber == null || entity.ReplyDate == null)
                throw new InvalidOperationException("لا رد مركزي مسجّل في المعاملة — أعدها إلى الديوان");

            var decision = (StudyDiwanDecision)request.Dto.CentralDecision;
            var note = StudyRules.Clean(request.Dto.Note);
            await _support.SignBranchHeadAsync(entity, viewer, StudyDecision.Approved, note);

            // جولة مغلقة: الصادر والرد وقراءة رئيس الفرع لقرار المركز
            entity.Rounds.Add(new StudyDiwanRound
            {
                RoundNumber = entity.RoundNumber,
                DispatchNumber = entity.DispatchNumber,
                DispatchDate = entity.DispatchDate,
                ReplyNumber = entity.ReplyNumber,
                ReplyDate = entity.ReplyDate.Value,
                CentralDecision = decision,
                ReferredByUserId = viewer.Id,
                ReferredAt = DateTime.Now,
                ReferralNote = note
            });
            entity.CentralDecision = decision;
            var approved = decision == StudyDiwanDecision.Approved;
            StudyDiwanTransactionSupport.MoveTo(entity, approved ? StudyDiwanStage.CentralApproved : StudyDiwanStage.CentralRejected);
            await _service.UpdateAsync(entity, request.Dto.RowVersion);

            var owner = ApprovalOwner.Of(entity);
            var closes = approved && entity.Response.Recommendation == StudyRecommendation.NotAvailable;
            await _kit.LogAsync(owner.RequestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Approved,
                $"أحال معاملة الديوان {entity.TransactionNumber} إلى مدير المشاريع: {StudyRules.Name(decision)} مركزية",
                $"الرد المركزي {entity.ReplyNumber}" + (note == null ? "" : $" — {note}")
                + (closes ? " — التوصية الفنية «غير متاح»: تُغلق السلسلة بلا أمر بدء" : ""));

            if (approved)
            {
                await _kit.NotifyNextStepAsync(StudyEntityType.DiwanTransaction, entity.Id);   // لمن يصدر أمر البدء
                await _kit.NotifyDecisionAsync(owner, true, note);                              // لموظف الديوان الذي أنشأها
            }
            else
            {
                await _kit.NotifyHoldersAsync(StudyPermissions.HandleDiwanRejection, owner.BranchId, null,
                    "رفض الديوان المركزي الدراسة — بانتظار إحالتك للتعديل",
                    $"{entity.TransactionNumber} — {entity.Response.Assignment.Request.MainBeneficiary}: {note}",
                    StudyEntityType.DiwanTransaction, entity.Id);
            }
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
