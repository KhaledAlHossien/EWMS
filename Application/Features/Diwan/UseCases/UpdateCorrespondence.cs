using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Diwan.DiwanSupport;

namespace Application.Features.Diwan
{
    public record UpdateCorrespondenceCommand(int Id, UpdateCorrespondenceRequestDto Dto) : IRequest<CorrespondenceResponseDto>;

    public class UpdateCorrespondenceCommandValidator : AbstractValidator<UpdateCorrespondenceCommand>
    {
        public UpdateCorrespondenceCommandValidator()
        {
            RuleFor(x => x.Dto.TypeId).GreaterThan(0).WithMessage("نوع المعاملة مطلوب");
            RuleFor(x => x.Dto).Custom((dto, context) =>
            {
                if (DiwanRules.ValidateRegistration(dto.Party, dto.Subject, dto.Summary, dto.ExternalNumber) is string problem) context.AddFailure(problem);
            });
        }
    }

    /// <summary>
    /// تعديل بيانات المعاملة (الموضوع والجهة والنوع ورقم الكتاب والمشروع…). الوارد المفتوح: لمن يحملها أو لمن سجّلها،
    /// والصادر: للمسودة فقط. تغيير نوع الوارد يعيد حساب موعده من تاريخ تسجيله بمهلة النوع الجديد. التغييرات سطراً سطراً في الأثر.
    /// </summary>
    public class UpdateCorrespondenceHandler(DiwanContext ctx) : IRequestHandler<UpdateCorrespondenceCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(UpdateCorrespondenceCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var c = await ctx.LoadAsync(viewer, request.Id);

            if (DiwanRules.IsIncoming(c))
            {
                if (DiwanRules.IsClosed(c)) throw new InvalidOperationException("المعاملة مغلقة: أعد فتحها أولاً");
                if (!DiwanRules.CanHandle(viewer, c) && !(viewer.Has(AppPermissions.RegisterCorrespondence) && c.RegisteredByUserId == viewer.Id))
                    throw new UnauthorizedAccessException("لا تحمل وحدتك هذه المعاملة الآن");
            }
            else DiwanRules.EnsureCanEditDraft(viewer, c);

            var type = dto.TypeId == c.TypeId ? c.Type : await ctx.ResolveTypeAsync(c.BranchId, dto.TypeId);
            var project = await ctx.ResolveProjectAsync(viewer, dto.ProjectId, c.BranchId);
            var replyTo = DiwanRules.IsIncoming(c) ? null : await ctx.ResolveReplyToAsync(viewer, dto.ReplyToId, c.BranchId);
            var external = (dto.ExternalNumber ?? string.Empty).Trim();

            var details = Lines(
                Change("الموضوع", c.Subject, dto.Subject),
                Change("الجهة", c.Party, dto.Party),
                Change("الملخص", c.Summary, dto.Summary),
                Change("النوع", c.Type.Name, type.Name),
                Change("رقم الكتاب", c.ExternalNumber, external),
                Change("تاريخ الكتاب", c.ExternalDate?.ToString("yyyy-MM-dd"), dto.ExternalDate?.ToString("yyyy-MM-dd")),
                Change("المشروع", c.Project?.Name, project?.Name),
                Change("ردّ على", c.ReplyToId?.ToString(), replyTo?.Id.ToString() ?? (DiwanRules.IsIncoming(c) ? c.ReplyToId?.ToString() : null)));

            c.Subject = dto.Subject.Trim();
            c.Party = dto.Party.Trim();
            c.Summary = (dto.Summary ?? string.Empty).Trim();
            c.ExternalNumber = external.Length == 0 ? null : external;
            c.ExternalDate = dto.ExternalDate?.Date;
            c.ProjectId = project?.Id;
            if (!DiwanRules.IsIncoming(c))
            {
                c.ReplyToId = replyTo?.Id;
                c.SubmittedAt = null;   // تغيّر ما رُفع للاعتماد: يُرفع من جديد
            }

            if (type.Id != c.TypeId)
            {
                c.TypeId = type.Id;
                if (DiwanRules.IsIncoming(c))
                {
                    var due = DiwanRules.DueDate(c.RegisteredAt.ToLocalTime(), type.DeadlineDays);
                    if (due != c.DueDate)
                    {
                        c.DueDate = due;
                        c.DueSoonNotifiedAt = null;      // موعد جديد: تنبيهاته من جديد
                        c.OverdueNotifiedAt = null;
                    }
                }
            }

            await ctx.Diwan.UpdateAsync(c, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Correspondence, c.Id, $"تعديل المعاملة {Label(c)}", details));
            return await ctx.DetailAsync(viewer, c.Id);
        }
    }
}
