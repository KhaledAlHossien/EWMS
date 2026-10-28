using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Diwan.DiwanSupport;

namespace Application.Features.Diwan
{
    // مسار الصادر: مسودة ← (رفع للاعتماد) ← اعتماد بتوقيع غير المُعدّ أو إعادة بسبب ← صدور برقم.

    public record SubmitOutgoingDraftCommand(int Id) : IRequest<CorrespondenceResponseDto>;
    public record DecideOutgoingDraftCommand(int Id, DecideOutgoingDraftRequestDto Dto) : IRequest<CorrespondenceResponseDto>;
    public record IssueOutgoingCommand(int Id) : IRequest<CorrespondenceResponseDto>;

    public class DecideOutgoingDraftCommandValidator : AbstractValidator<DecideOutgoingDraftCommand>
    {
        public DecideOutgoingDraftCommandValidator() =>
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }

    /// <summary>رفع المسودة للاعتماد: تشترط مرفقاً (نص الكتاب)، وتُشعَر من يملك اعتماد الصادر في الفرع. لا تغيّر حالة المسودة.</summary>
    public class SubmitOutgoingDraftHandler(DiwanContext ctx) : IRequestHandler<SubmitOutgoingDraftCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(SubmitOutgoingDraftCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var c = await ctx.LoadAsync(viewer, request.Id);
            DiwanRules.EnsureCanEditDraft(viewer, c);
            if (c.Attachments.Count < 1) throw new InvalidOperationException("أرفق نص الكتاب (ملفاً واحداً على الأقل) قبل رفعه للاعتماد");

            c.ReturnNote = null;
            await ctx.Diwan.UpdateAsync(c, null, new AuditEntry(viewer.Id, AuditActions.Submitted, AuditEntities.Correspondence, c.Id,
                $"رفع المسودة للاعتماد {Label(c)}"));

            var approvers = (await ctx.Permissions.GetUsersWithPermissionAsync(AppPermissions.ApproveOutgoingCorrespondence, branchId: c.BranchId)).Select(u => u.Id);
            await ctx.NotifyAsync(approvers, viewer.Id, c, NotificationType.OutgoingAwaitingApproval, "مسودة صادر بانتظار اعتمادك",
                $"رفع {viewer.User.FullName} مسودة الكتاب «{c.Subject}» إلى «{c.Party}» للاعتماد");
            return await ctx.DetailAsync(viewer, c.Id);
        }
    }

    /// <summary>
    /// اعتماد المسودة بتوقيع المعتمِد (إصدار توقيعه الحالي يُثبَّت مع القرار) أو إعادتها بسبب. المعتمِد ليس من أعدّها،
    /// ولا يُعتمد بلا مرفق. يُشعَر من أعدّها بالنتيجة.
    /// </summary>
    public class DecideOutgoingDraftHandler(DiwanContext ctx) : IRequestHandler<DecideOutgoingDraftCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(DecideOutgoingDraftCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var c = await ctx.LoadAsync(viewer, request.Id);
            DiwanRules.EnsureCanDecideDraft(viewer, c, c.Attachments.Count, dto.Approve, dto.Note);

            var drafterId = c.RegisteredByUserId;
            if (dto.Approve)
            {
                c.OutgoingState = OutgoingState.Approved;
                c.ApprovedByUserId = viewer.Id;
                c.ApprovedAt = DateTime.UtcNow;
                c.ApprovedSignatureId = await ctx.Signatures.GetCurrentIdAsync(viewer.Id);   // null = اعتُمد بلا توقيع مرفوع
                c.ReturnNote = null;
            }
            else
            {
                c.ReturnNote = dto.Note!.Trim();
            }

            await ctx.Diwan.UpdateAsync(c, dto.RowVersion, new AuditEntry(viewer.Id, dto.Approve ? AuditActions.Approved : AuditActions.Returned,
                AuditEntities.Correspondence, c.Id, $"{(dto.Approve ? "اعتماد" : "إعادة")} مسودة الصادر {Label(c)}", dto.Approve ? string.Empty : c.ReturnNote!));

            await ctx.NotifyAsync([drafterId], viewer.Id, c, NotificationType.OutgoingDecided,
                dto.Approve ? "اعتُمدت مسودة الصادر" : "أُعيدت مسودة الصادر إليك",
                dto.Approve ? $"اعتمد {viewer.User.FullName} الكتاب «{c.Subject}» وهو جاهز للإصدار" : $"أعاد {viewer.User.FullName} الكتاب «{c.Subject}»: {c.ReturnNote}");
            return await ctx.DetailAsync(viewer, c.Id);
        }
    }

    /// <summary>إصدار الصادر المعتمَد: يأخذ تسلسله في سنة الإصدار ويُقفل أرشيفاً. يصدره الديوان (RegisterCorrespondence) في الفرع.</summary>
    public class IssueOutgoingHandler(DiwanContext ctx) : IRequestHandler<IssueOutgoingCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(IssueOutgoingCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var c = await ctx.LoadAsync(viewer, request.Id);
            DiwanRules.EnsureCanIssue(viewer, c);

            await ctx.Diwan.IssueAsync(c, viewer.Id, new AuditEntry(viewer.Id, AuditActions.Issued, AuditEntities.Correspondence, c.Id,
                $"إصدار الكتاب الصادر «{c.Subject}»", $"إلى: {c.Party}"));
            return await ctx.DetailAsync(viewer, c.Id);
        }
    }
}
