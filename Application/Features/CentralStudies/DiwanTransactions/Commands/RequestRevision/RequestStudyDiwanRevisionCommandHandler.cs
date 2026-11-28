using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.RequestRevision
{
    public class RequestStudyDiwanRevisionCommandHandler : IRequestHandler<RequestStudyDiwanRevisionCommand, StudyDiwanTransactionDto>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly IStudyApprovalService _approvals;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public RequestStudyDiwanRevisionCommandHandler(IStudyDiwanTransactionService service, IStudyApprovalService approvals,
            StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _approvals = approvals;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDiwanTransactionDto> Handle(RequestStudyDiwanRevisionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyDiwanTransactionSupport.EnsureStage(entity, StudyDiwanStage.CentralRejected, "يُختار التعديل بعد إحالة المعاملة برفض مركزي");
            StudyDiwanTransactionSupport.EnsureResponseApproved(entity);
            var round = StudyRules.LastRound(entity);
            if (round is not { CentralDecision: StudyDiwanDecision.Rejected, RevisionKind: null })
                throw new InvalidOperationException("لا رفض مركزي بانتظار الإحالة في هذه المعاملة");

            var kind = (StudyDiwanRevision)request.Dto.Kind;
            var reason = request.Dto.Reason.Trim();
            var response = entity.Response;
            if (kind == StudyDiwanRevision.Planning)
            {
                // يبقى توقيع القسم المتابع (جزؤه لم يتغير)، ويعود سطر التخطيط وحده إلى الانتظار فيعدّل مدير المشاريع جزءه ويوقّعه
                var rows = await _approvals.GetTrackedForOwnerAsync(StudyEntityType.Response, response.Id);
                var following = rows.FirstOrDefault(r => r.SignerRole == StudySignerRole.FollowingDepartment);
                var planning = rows.FirstOrDefault(r => r.SignerRole == StudySignerRole.PlanningAndProjects);
                if (following == null || !StudyRules.IsPositive(following.Decision) || planning == null)
                    throw new InvalidOperationException("لا توقيع للقسم المتابع محفوظ على إشعار الرد — اختر التعديل الفني");
                planning.Decision = StudyDecision.Pending;
                planning.SignerUserId = null;
                planning.SignatureId = null;
                planning.JobTitle = null;
                planning.DecidedAt = null;
                planning.Note = null;
                response.Status = StudyDocumentStatus.PendingApproval;
            }
            else
            {
                response.Status = StudyDocumentStatus.Rejected;   // يعدّله القسم المتابع ثم يرسله فيوقّع هو ثم التخطيط
            }
            response.UpdatedAt = DateTime.Now;
            round.RevisionKind = kind;
            round.RevisionByUserId = viewer.Id;
            round.RevisionAt = DateTime.Now;
            round.RevisionReason = reason;
            StudyDiwanTransactionSupport.MoveTo(entity,
                kind == StudyDiwanRevision.Planning ? StudyDiwanStage.PlanningRevision : StudyDiwanStage.TechnicalRevision);
            await _service.UpdateAsync(entity, request.Dto.RowVersion);

            var requestId = response.Assignment.RequestId;
            var branchId = response.Assignment.Request.BranchId;
            await _kit.LogAsync(requestId, StudyEntityType.DiwanTransaction, entity.Id, StudyLogAction.Edited,
                $"أحال الرفض المركزي في معاملة الديوان {entity.TransactionNumber} إلى {StudyRules.Name(kind)}", reason);
            var message = $"{response.ResponseNumber} — {response.Assignment.Request.MainBeneficiary}: {reason}";
            if (kind == StudyDiwanRevision.Planning)
            {
                await _kit.LogAsync(requestId, StudyEntityType.Response, response.Id, StudyLogAction.Edited,
                    $"أُعيد إشعار الرد {response.ResponseNumber} إلى توقيع التخطيط لتعديل تخطيطي بعد رفض مركزي", reason);
                await _kit.NotifyHoldersAsync(StudyPermissions.EditResponsePlanning, branchId, null,
                    "رفض مركزي — تعديل تخطيطي مطلوب على إشعار الرد", message, StudyEntityType.Response, response.Id);
                await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(response));
            }
            else
            {
                await _kit.LogAsync(requestId, StudyEntityType.Response, response.Id, StudyLogAction.Rejected,
                    $"أُعيد إشعار الرد {response.ResponseNumber} إلى القسم المتابع لتعديل فني بعد رفض مركزي", reason);
                await _kit.NotifyHoldersAsync(StudyPermissions.EditResponse, branchId, response.Assignment.FollowingDepartmentId,
                    "رفض مركزي — تعديل فني مطلوب على إشعار الرد", message, StudyEntityType.Response, response.Id,
                    [response.CreatedByUserId]);
            }
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
