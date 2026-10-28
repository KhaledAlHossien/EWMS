using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Studies.StudySupport;

namespace Application.Features.Studies
{
    // الإرسال إلى المركزية وقرارها: يدويان (لا ربط إلكتروني معها)، بتاريخ وملاحظات ووثيقة قرار اختيارية.

    public record SendStudyCommand(int StudyOrderId, SendStudyRequestDto Dto) : IRequest<StudyDetailDto>;
    public record DecideStudyCommand(int SubmissionId, DecideStudyRequestDto Dto) : IRequest<StudyDetailDto>;

    public class SendStudyCommandValidator : AbstractValidator<SendStudyCommand>
    {
        public SendStudyCommandValidator() =>
            RuleFor(x => x.Dto.Note).MaximumLength(StudyRules.MaxNoteLength).WithMessage($"الملاحظة لا تتجاوز {StudyRules.MaxNoteLength} حرفاً");
    }

    public class DecideStudyCommandValidator : AbstractValidator<DecideStudyCommand>
    {
        public DecideStudyCommandValidator()
        {
            RuleFor(x => x.Dto.Decision).Must(StudyRules.IsValidDecision).WithMessage("القرار اعتماد أو ملاحظات");
            RuleFor(x => x.Dto.Note).MaximumLength(StudyRules.MaxNoteLength).WithMessage($"الملاحظات لا تتجاوز {StudyRules.MaxNoteLength} حرفاً");
        }
    }

    /// <summary>الإرسال: بعد اعتماد كل المخرجات؛ يفتح جولة جديدة بانتظار القرار ويحوّل الأمر إلى «أُرسلت للمركزية»</summary>
    public class SendStudyHandler(StudyContext ctx) : IRequestHandler<SendStudyCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(SendStudyCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var order = await ctx.LoadAsync(viewer, request.StudyOrderId);
            StudyRules.EnsureCanSend(viewer, order);

            Correspondence? letter = null;
            if (dto.OutgoingCorrespondenceId is int letterId)
            {
                letter = await ctx.Diwan.GetByIdAsync(letterId) ?? throw new KeyNotFoundException("الكتاب الصادر غير موجود");
                Diwan.DiwanRules.EnsureView(viewer, letter);
                if (letter.Direction != CorrespondenceDirection.Outgoing || letter.BranchId != order.Project.BranchId)
                    throw new InvalidOperationException("اختر كتاباً صادراً من فرع المشروع");
            }

            var round = StudyRules.NextRound(order);
            order.Status = StudyStatus.SentToCenter;
            await ctx.Studies.AddSubmissionAsync(order, new StudySubmission
            {
                Round = round, SentAt = DateTime.UtcNow, SentByUserId = viewer.Id, OutgoingCorrespondenceId = letter?.Id,
                Note = (dto.Note ?? string.Empty).Trim(), Decision = CenterDecision.Pending
            }, new AuditEntry(viewer.Id, AuditActions.Sent, AuditEntities.Submission, null,
                $"إرسال الدراسة {StudyContext.Label(order)} إلى المركزية (الجولة {round})", dto.Note?.Trim() ?? string.Empty));

            var recipients = (await ctx.HoldersAsync(order.Project, AppPermissions.EditStudyOrder)).Append(order.CreatedByUserId);
            await ctx.NotifyAsync(recipients, viewer.Id, order, NotificationType.StudySentToCenter, "أُرسلت دراسة إلى المركزية",
                $"أرسل {viewer.User.FullName} الدراسة {StudyContext.Label(order)} إلى المركزية (الجولة {round})");
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }

    /// <summary>قرار المركزية على الجولة المعلّقة: اعتماد (تُعتمد الدراسة) أو ملاحظات (تُعاد لجولة جديدة)</summary>
    public class DecideStudyHandler(StudyContext ctx) : IRequestHandler<DecideStudyCommand, StudyDetailDto>
    {
        public async Task<StudyDetailDto> Handle(DecideStudyCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var order = await ctx.Studies.GetBySubmissionAsync(request.SubmissionId) ?? throw new KeyNotFoundException("الإرسال غير موجود");
            StudyRules.EnsureCanView(viewer, order.Project);
            var submission = order.Submissions.First(s => s.Id == request.SubmissionId);
            var decision = (CenterDecision)dto.Decision;
            StudyRules.EnsureCanDecide(viewer, order, submission, decision, dto.Note);

            ProjectDocumentVersion? version = null;
            if (dto.DecisionVersionId is int versionId)
            {
                version = await ctx.Studies.GetVersionAsync(versionId) ?? throw new KeyNotFoundException("إصدار وثيقة القرار غير موجود");
                if (version.Document.ProjectId != order.ProjectId) throw new InvalidOperationException("وثيقة القرار من مشروع آخر");
            }

            submission.Decision = decision;
            submission.DecidedAt = DateTime.UtcNow;
            submission.DecidedByUserId = viewer.Id;
            submission.DecisionNote = (dto.Note ?? string.Empty).Trim();
            submission.DecisionVersionId = version?.Id;
            order.Status = StudyRules.StatusAfter(decision);

            await ctx.Studies.DecideSubmissionAsync(order, submission, dto.RowVersion, new AuditEntry(viewer.Id, AuditActions.Decided, AuditEntities.Submission, submission.Id,
                $"تسجيل قرار المركزية على {StudyContext.Label(order)} (الجولة {submission.Round}): {StudyRules.DecisionAr(decision)}", submission.DecisionNote));

            var recipients = (await ctx.HoldersAsync(order.Project, AppPermissions.EditStudyOrder)).Append(order.CreatedByUserId);
            await ctx.NotifyAsync(recipients, viewer.Id, order, NotificationType.StudyCenterDecided,
                decision == CenterDecision.Approved ? "اعتمدت المركزية دراسة" : "أعادت المركزية دراسة بملاحظات",
                $"{StudyRules.DecisionAr(decision)}: {StudyContext.Label(order)}" + (submission.DecisionNote.Length > 0 ? $" — {submission.DecisionNote}" : string.Empty));
            return await ctx.DetailAsync(viewer, order.Id);
        }
    }
}
