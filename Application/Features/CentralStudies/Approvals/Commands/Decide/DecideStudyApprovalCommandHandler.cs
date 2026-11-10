using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Approvals.Commands.Decide
{
    public class DecideStudyApprovalCommandHandler : IRequestHandler<DecideStudyApprovalCommand, StudyApprovalDto>
    {
        private readonly IStudyApprovalService _service;
        private readonly IUserSignatureService _signatures;
        private readonly StudyKit _kit;

        public DecideStudyApprovalCommandHandler(IStudyApprovalService service, IUserSignatureService signatures, StudyKit kit)
        {
            _service = service;
            _signatures = signatures;
            _kit = kit;
        }

        public async Task<StudyApprovalDto> Handle(DecideStudyApprovalCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var row = await _service.GetWithOwnerAsync(request.Id) ?? throw new KeyNotFoundException("سطر التوقيع غير موجود");
            var owner = ApprovalOwner.Of(row);
            StudyRules.EnsureInScope(viewer, owner.BranchId);

            var siblings = await _service.GetForOwnerAsync(owner.Type, owner.Id);
            var current = siblings.First(s => s.Id == row.Id);
            if (!StudyKit.CanDecide(viewer, current, siblings, owner))
                throw new UnauthorizedAccessException(Explain(viewer, row, siblings, owner));

            var decision = (StudyDecision)request.Dto.Decision;
            var now = DateTime.Now;
            row.Decision = decision;
            row.SignerUserId = viewer.Id;
            row.JobTitle = viewer.User.Role?.Name;
            row.SignatureId = await _signatures.GetCurrentIdAsync(viewer.Id);
            row.DecidedAt = now;
            row.Note = string.IsNullOrWhiteSpace(request.Dto.Note) ? null : request.Dto.Note.Trim();

            // حالة المستند بعد القرار
            var decisions = siblings.Select(s => s.Id == row.Id ? decision : s.Decision).ToList();
            bool? final = decision == StudyDecision.Rejected ? false
                : decisions.All(StudyRules.IsPositive) ? true
                : null;
            if (final is bool approved) ApplyFinal(row, approved, decisions, now);

            await _service.SaveAsync();

            var verb = decision == StudyDecision.Rejected ? StudyLogAction.Rejected : StudyLogAction.Approved;
            await _kit.LogAsync(owner.RequestId, owner.Type, owner.Id, verb,
                $"{StudyRules.Name(decision)} بصفة {StudyRules.Name(row.SignerRole)} على {StudyRules.Name(owner.Type)} {owner.Number}", row.Note);
            if (final is bool done)
            {
                if (done)
                {
                    await _kit.LogAsync(owner.RequestId, owner.Type, owner.Id, StudyLogAction.Approved,
                        $"اعتُمد {StudyRules.Name(owner.Type)} {owner.Number} نهائياً");
                    await _kit.NotifyNextStepAsync(owner.Type, owner.Id);   // لمن يصدر المستند التالي
                }
                await _kit.NotifyDecisionAsync(owner, done, row.Note);
            }
            else
            {
                await _kit.NotifyNextSignersAsync(owner with { Pending = true });
            }

            var updated = (await _service.GetForOwnerAsync(owner.Type, owner.Id)).First(s => s.Id == row.Id);
            var dto = _kit.Mapper.Map<StudyApprovalDto>(updated);
            return dto;
        }

        /// <summary>القرار النهائي ينتقل إلى المستند (وللـ CDA إلى النسخة والمخطط، وللمشروع يكتمل)</summary>
        private static void ApplyFinal(StudyApproval row, bool approved, List<StudyDecision> decisions, DateTime now)
        {
            var status = approved ? StudyDocumentStatus.Approved : StudyDocumentStatus.Rejected;
            if (row.Assignment != null) { row.Assignment.Status = status; row.Assignment.UpdatedAt = now; }
            else if (row.Response != null) { row.Response.Status = status; row.Response.UpdatedAt = now; }
            else if (row.StartOrder != null) { row.StartOrder.Status = status; row.StartOrder.UpdatedAt = now; }
            else if (row.DesignApproval is { } cda)
            {
                cda.Decision = !approved ? StudyDecision.Rejected
                    : decisions.Contains(StudyDecision.ApprovedWithNotes) ? StudyDecision.ApprovedWithNotes
                    : StudyDecision.Approved;
                cda.UpdatedAt = now;
                cda.Version.Status = approved ? DrawingStatus.Approved : DrawingStatus.Rejected;
                // مخطط له نسخة معتمدة سابقاً يبقى معتمداً إن رُفضت نسخة لاحقة
                if (approved || cda.Version.Drawing.Status != DrawingStatus.Approved)
                    cda.Version.Drawing.Status = cda.Version.Status;
                cda.Version.Drawing.UpdatedAt = now;
            }
            else if (row.Project is { } project && approved)
            {
                project.Status = StudyProjectStatus.Completed;
                project.ActualEndDate ??= now.Date;
                project.UpdatedAt = now;
            }
        }

        /// <summary>سبب واضح لرفض التوقيع</summary>
        private static string Explain(Common.Viewer viewer, StudyApproval row, List<StudyApproval> siblings, ApprovalOwner owner)
        {
            if (!owner.Pending) return "المستند ليس بانتظار الاعتماد";
            if (row.Decision != StudyDecision.Pending) return "صدر القرار على هذا السطر مسبقاً";
            if (!viewer.Has(StudyRules.SignPermission(row.SignerRole))) return $"لا تملك صلاحية التوقيع بصفة {StudyRules.Name(row.SignerRole)}";
            if (StudyRules.IsDepartmentRole(row.SignerRole) && !StudyRules.InDepartment(viewer, owner.RoleDepartmentId))
                return $"يوقّع بصفة {StudyRules.Name(row.SignerRole)} موظفو القسم المعني بالمستند فقط";
            if (siblings.Any(s => s.SortOrder < row.SortOrder && !StudyRules.IsPositive(s.Decision))) return "لم يوقّع من قبلك في الترتيب بعد";
            return "وقّعت على هذا المستند بصفة أخرى — لا يوقّع الشخص مرتين على المستند نفسه";
        }
    }
}
