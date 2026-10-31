using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Pricing.PricingSupport;

namespace Application.Features.Pricing
{
    // مسار طلب التسعير بعد جمع العروض: توثيق التوصية (المكلَّف)، القرار النهائي (غير من أوصى)، وإرسال النتيجة إلى الجهة المختصة.

    public record RecommendPricingRequestCommand(int Id, PricingStepRequestDto Dto) : IRequest<PricingDetailDto>;
    public record DecidePricingRequestCommand(int Id, DecidePricingRequestDto Dto) : IRequest<PricingDetailDto>;
    public record SendPricingResultCommand(int Id, SendPricingResultRequestDto Dto) : IRequest<PricingDetailDto>;

    /// <summary>توثيق التوصية: لكل بند سطر مختار ونص التوصية؛ يتحول الطلب إلى «بانتظار الاعتماد» ويُنبَّه المعتمِدون</summary>
    public class RecommendPricingRequestHandler(PricingContext ctx) : IRequestHandler<RecommendPricingRequestCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(RecommendPricingRequestCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var item = await ctx.LoadAsync(viewer, request.Id);
            PricingRules.EnsureCanRecommend(viewer, item, dto.Note);

            item.Status = PricingRequestStatus.Recommended;
            item.RecommendationNote = dto.Note!.Trim();
            item.RecommendedByUserId = viewer.Id;
            item.RecommendedAt = DateTime.UtcNow;
            var totals = string.Join("، ", PricingRules.SelectedTotals(item).OrderBy(t => t.Key).Select(t => $"{t.Value:0.##} {t.Key}"));
            await ctx.Pricing.UpdateAsync(item, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Submitted, AuditEntities.PricingRequest, item.Id, $"توثيق التوصية في طلب التسعير {PricingContext.Label(item)}",
                    Lines($"إجمالي المختار: {totals}", $"التوصية: {dto.Note.Trim()}")));

            await ctx.NotifyRequestAsync(await ctx.HoldersAsync(item.BranchId, AppPermissions.ApprovePricingRequest), viewer.Id, item, NotificationType.PricingRecommendationAwaiting,
                "توصية تسعير بانتظار اعتمادك", $"أوصى {viewer.User.FullName} في الطلب {PricingContext.Label(item)}");
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }

    /// <summary>
    /// القرار على التوصية: اعتماد، أو رفض بسبب، أو إعادة لجمع العروض بسبب (فتُمسح التوصية ويبقى الاختيار قائماً للتعديل). غير من أوصى.
    /// </summary>
    public class DecidePricingRequestHandler(PricingContext ctx) : IRequestHandler<DecidePricingRequestCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(DecidePricingRequestCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var item = await ctx.LoadAsync(viewer, request.Id);
            var decision = (PricingRules.Decision)dto.Decision;
            PricingRules.EnsureCanDecide(viewer, item, decision, dto.Note);

            var recommender = item.RecommendedByUserId;
            item.Status = PricingRules.StatusAfter(decision);
            item.DecidedByUserId = viewer.Id;
            item.DecidedAt = DateTime.UtcNow;
            item.DecisionNote = (dto.Note ?? string.Empty).Trim();
            if (decision == PricingRules.Decision.Return)
            {
                // تُعاد التوصية ليوثّقها المكلَّف من جديد بعد التعديل
                item.RecommendedByUserId = null;
                item.RecommendedAt = null;
            }
            var word = decision switch { PricingRules.Decision.Approve => "اعتماد", PricingRules.Decision.Reject => "رفض", _ => "إعادة لجمع العروض" };
            await ctx.Pricing.UpdateAsync(item, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Decided, AuditEntities.PricingRequest, item.Id, $"{word} طلب التسعير {PricingContext.Label(item)}",
                    string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $"الملاحظة: {dto.Note.Trim()}"));

            var recipients = new List<int>();
            if (recommender is int r) recipients.Add(r);
            if (item.AssigneeUserId is int a) recipients.Add(a);
            await ctx.NotifyRequestAsync(recipients, viewer.Id, item, NotificationType.PricingRequestDecided, $"{word} طلب تسعير",
                $"{word} {viewer.User.FullName} الطلب {PricingContext.Label(item)}" + (string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $": {dto.Note.Trim()}"));
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }

    /// <summary>تسجيل إرسال النتيجة (المعتمدة أو المرفوضة) إلى الجهة المختصة بتسميتها وتاريخها؛ يُنبَّه مقدّم الطلب</summary>
    public class SendPricingResultHandler(PricingContext ctx) : IRequestHandler<SendPricingResultCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(SendPricingResultCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var item = await ctx.LoadAsync(viewer, request.Id);
            PricingRules.EnsureCanSendResult(viewer, item, dto.Recipient, dto.Note);

            var outcome = PricingRules.StatusAr(item.Status);
            item.Status = PricingRequestStatus.ResultSent;
            item.ResultRecipient = dto.Recipient.Trim();
            item.ResultSentAt = DateTime.UtcNow;
            item.ResultSentByUserId = viewer.Id;
            item.ResultNote = (dto.Note ?? string.Empty).Trim();
            await ctx.Pricing.UpdateAsync(item, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Sent, AuditEntities.PricingRequest, item.Id, $"إرسال نتيجة طلب التسعير {PricingContext.Label(item)} إلى {dto.Recipient.Trim()}",
                    Lines($"النتيجة: {outcome}", string.IsNullOrWhiteSpace(dto.Note) ? null : $"الملاحظة: {dto.Note.Trim()}")));

            await ctx.NotifyRequestAsync([item.RegisteredByUserId], viewer.Id, item, NotificationType.PricingResultSent, "أُرسلت نتيجة طلب التسعير",
                $"أُرسلت نتيجة الطلب {PricingContext.Label(item)} ({outcome}) إلى {dto.Recipient.Trim()}");
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }
}
