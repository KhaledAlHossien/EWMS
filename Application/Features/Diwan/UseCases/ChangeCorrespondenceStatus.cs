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
    public record ChangeCorrespondenceStatusCommand(int Id, ChangeCorrespondenceStatusRequestDto Dto) : IRequest<CorrespondenceResponseDto>;

    public class ChangeCorrespondenceStatusCommandValidator : AbstractValidator<ChangeCorrespondenceStatusCommand>
    {
        public ChangeCorrespondenceStatusCommandValidator() =>
            RuleFor(x => x.Dto.StatusId).GreaterThan(0).WithMessage("اختر الحالة");
    }

    /// <summary>
    /// تغيير حالة الوارد إلى حالة فعّالة من فرعه، ممّن تحمل وحدته المعاملة. الانتقال إلى مرحلة «مغلقة» وإعادة الفتح بصلاحية الإغلاق،
    /// ويُسجَّلان بوقتهما وصاحبهما. لا تغيّر الحالة أي تأخير: النظام لا يغلق ولا يعيد فتح معاملة وحده.
    /// </summary>
    public class ChangeCorrespondenceStatusHandler(DiwanContext ctx) : IRequestHandler<ChangeCorrespondenceStatusCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(ChangeCorrespondenceStatusCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var c = await ctx.LoadAsync(viewer, request.Id);
            var target = await ctx.Diwan.GetStatusAsync(request.Dto.StatusId) ?? throw new KeyNotFoundException("الحالة غير موجودة");
            DiwanRules.EnsureCanChangeStatus(viewer, c, target);
            if (c.StatusId == target.Id) return await ctx.DetailAsync(viewer, c.Id);

            var wasClosed = DiwanRules.IsClosed(c);
            var closing = target.Stage == CorrespondenceStage.Closed;
            var from = c.Status?.Name;

            c.StatusId = target.Id;
            if (closing)
            {
                c.ClosedAt = DateTime.UtcNow;
                c.ClosedByUserId = viewer.Id;
            }
            else if (wasClosed)
            {
                c.ClosedAt = null;
                c.ClosedByUserId = null;
            }

            var action = closing ? AuditActions.Closed : wasClosed ? AuditActions.Reopened : AuditActions.Stage;
            await ctx.Diwan.UpdateAsync(c, request.Dto.RowVersion, new AuditEntry(viewer.Id, action, AuditEntities.Correspondence, c.Id,
                $"{(closing ? "إغلاق" : wasClosed ? "إعادة فتح" : "تغيير حالة")} المعاملة {Label(c)}", Change("الحالة", from, target.Name) ?? string.Empty));
            return await ctx.DetailAsync(viewer, c.Id);
        }
    }
}
