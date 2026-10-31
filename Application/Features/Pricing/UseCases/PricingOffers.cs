using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Pricing.PricingSupport;

namespace Application.Features.Pricing
{
    // عروض الأسعار وأسعار بنودها (يعمل عليها المكلَّف بالطلب): التسجيل والتعديل، ثم اختيار سطر لكل بند بسبب مكتوب أو إلغاء الاختيار.
    // لا اختيار آلي للأرخص، ولا لما لم تتحقق مطابقته أو يخالف وحدة البند أو انتهت صلاحيته.

    public record AddPricingOfferCommand(int RequestId, PricingOfferRequestDto Dto) : IRequest<PricingDetailDto>;
    public record UpdatePricingOfferCommand(int OfferId, PricingOfferRequestDto Dto) : IRequest<PricingDetailDto>;
    public record SelectPricingLineCommand(int LineId, SelectPricingLineRequestDto Dto) : IRequest<PricingDetailDto>;
    public record UnselectPricingLineCommand(int LineId) : IRequest<PricingDetailDto>;

    internal static class OfferInput
    {
        public static List<PricingRules.LineInput> Inputs(PricingOfferRequestDto dto) =>
            dto.Lines.Select(l => new PricingRules.LineInput(l.ItemId, l.UnitPrice, l.Unit, l.Conformity, l.ConformityNote)).ToList();

        public static PricingOfferLine NewLine(PricingLineRequestDto l) => new()
        {
            PricingRequestItemId = l.ItemId, UnitPrice = l.UnitPrice, Unit = l.Unit.Trim(), Conformity = (ConformityResult)l.Conformity, ConformityNote = (l.ConformityNote ?? string.Empty).Trim()
        };

        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class AddPricingOfferHandler(PricingContext ctx) : IRequestHandler<AddPricingOfferCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(AddPricingOfferCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var item = await ctx.LoadAsync(viewer, request.RequestId);
            var supplier = await ctx.ResolveSupplierAsync(dto.SupplierId);
            PricingRules.EnsureCanAddOffer(viewer, item, supplier);
            if (PricingRules.ValidateOffer(dto.OfferNumber, dto.Currency, dto.ReceivedAt, dto.ValidUntil, dto.SupplyTerms, dto.Note, DateTime.Today) is string offerProblem)
                throw new InvalidOperationException(offerProblem);
            if (PricingRules.ValidateLines(item, OfferInput.Inputs(dto)) is string lineProblem) throw new InvalidOperationException(lineProblem);
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, item.ProjectId);

            var offer = new PricingOffer
            {
                PricingRequestId = item.Id, SupplierId = supplier.Id, OfferNumber = OfferInput.Clean(dto.OfferNumber), ReceivedAt = dto.ReceivedAt.Date, ValidUntil = dto.ValidUntil?.Date,
                Currency = dto.Currency, SupplyTerms = (dto.SupplyTerms ?? string.Empty).Trim(), Note = (dto.Note ?? string.Empty).Trim(), DocumentVersionId = version?.Id,
                RecordedByUserId = viewer.Id, RecordedAt = DateTime.UtcNow, Lines = dto.Lines.Select(OfferInput.NewLine).ToList()
            };
            await ctx.Pricing.AddOfferAsync(item, offer, new AuditEntry(viewer.Id, AuditActions.Recorded, AuditEntities.PricingRequest, item.Id,
                $"تسجيل عرض «{supplier.Name}» على الطلب {PricingContext.Label(item)}", $"العملة: {dto.Currency} — البنود المسعَّرة: {dto.Lines.Count}"));
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }

    /// <summary>
    /// تعديل العرض: تُحدَّث أسعار البنود المرسلة وتُضاف الجديدة وتُحذف ما لم تُرسَل. السطر المختار لا يتغير ولا يُحذف (ألغِ اختياره أولاً)،
    /// والمورد لا يتغير، وعملة عرض فيه سطر مختار لا تتغير.
    /// </summary>
    public class UpdatePricingOfferHandler(PricingContext ctx) : IRequestHandler<UpdatePricingOfferCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(UpdatePricingOfferCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (item, offer) = await ctx.LoadOfferAsync(viewer, request.OfferId);
            PricingRules.EnsureCanWork(viewer, item);
            if (dto.SupplierId != offer.SupplierId) throw new InvalidOperationException("المورد لا يتغير: سجّل عرضاً جديداً");
            if (PricingRules.ValidateOffer(dto.OfferNumber, dto.Currency, dto.ReceivedAt, dto.ValidUntil, dto.SupplyTerms, dto.Note, DateTime.Today) is string offerProblem)
                throw new InvalidOperationException(offerProblem);
            if (PricingRules.ValidateLines(item, OfferInput.Inputs(dto)) is string lineProblem) throw new InvalidOperationException(lineProblem);
            if (dto.Currency != offer.Currency && offer.Lines.Any(l => l.IsSelected)) throw new InvalidOperationException("في العرض سطر مختار: ألغِ اختياره قبل تغيير العملة");
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, item.ProjectId);

            var sent = dto.Lines.ToDictionary(l => l.ItemId);
            var removed = new List<PricingOfferLine>();
            foreach (var line in offer.Lines.ToList())
            {
                if (!sent.TryGetValue(line.PricingRequestItemId, out var s))
                {
                    PricingRules.EnsureLineEditable(line);
                    removed.Add(line);
                    continue;
                }
                var changed = line.UnitPrice != s.UnitPrice || !PricingRules.SameUnit(line.Unit, s.Unit) || (int)line.Conformity != s.Conformity;
                if (changed) PricingRules.EnsureLineEditable(line);
                line.UnitPrice = s.UnitPrice;
                line.Unit = s.Unit.Trim();
                line.Conformity = (ConformityResult)s.Conformity;
                line.ConformityNote = (s.ConformityNote ?? string.Empty).Trim();
            }
            var existing = offer.Lines.Select(l => l.PricingRequestItemId).ToHashSet();
            foreach (var s in dto.Lines.Where(l => !existing.Contains(l.ItemId))) offer.Lines.Add(OfferInput.NewLine(s));

            var details = Lines(Change("العملة", offer.Currency, dto.Currency), Change("رقم العرض", offer.OfferNumber, dto.OfferNumber),
                Change("الصلاحية", offer.ValidUntil?.ToString("yyyy-MM-dd"), dto.ValidUntil?.ToString("yyyy-MM-dd")), Change("شروط التوريد", offer.SupplyTerms, dto.SupplyTerms),
                $"أسطر العرض: {dto.Lines.Count}");
            offer.OfferNumber = OfferInput.Clean(dto.OfferNumber);
            offer.ReceivedAt = dto.ReceivedAt.Date;
            offer.ValidUntil = dto.ValidUntil?.Date;
            offer.Currency = dto.Currency;
            offer.SupplyTerms = (dto.SupplyTerms ?? string.Empty).Trim();
            offer.Note = (dto.Note ?? string.Empty).Trim();
            offer.DocumentVersionId = version?.Id;

            await ctx.Pricing.SaveOfferAsync(item, offer, removed, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.PricingRequest, item.Id, $"تعديل عرض «{offer.Supplier.Name}» على الطلب {PricingContext.Label(item)}", details));
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }

    public class SelectPricingLineHandler(PricingContext ctx) : IRequestHandler<SelectPricingLineCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(SelectPricingLineCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var (item, offer, line) = await ctx.LoadLineAsync(viewer, request.LineId);
            var requestItem = item.Items.First(i => i.Id == line.PricingRequestItemId);
            PricingRules.EnsureCanSelect(viewer, item, requestItem, offer, line, request.Dto.Reason, DateTime.Today);

            var previous = item.Offers.SelectMany(o => o.Lines).FirstOrDefault(l => l.PricingRequestItemId == requestItem.Id && l.IsSelected);
            await ctx.Pricing.SelectLineAsync(item, line, previous, request.Dto.Reason.Trim(),
                new AuditEntry(viewer.Id, AuditActions.Decided, AuditEntities.PricingRequest, item.Id,
                    $"اختيار عرض «{offer.Supplier.Name}» للبند «{requestItem.Description}» في الطلب {PricingContext.Label(item)}",
                    Lines($"السعر: {line.UnitPrice:0.####} {offer.Currency} / {line.Unit}", $"السبب: {request.Dto.Reason.Trim()}")));
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }

    public class UnselectPricingLineHandler(PricingContext ctx) : IRequestHandler<UnselectPricingLineCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(UnselectPricingLineCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var (item, offer, line) = await ctx.LoadLineAsync(viewer, request.LineId);
            PricingRules.EnsureCanUnselect(viewer, item, line);

            var requestItem = item.Items.First(i => i.Id == line.PricingRequestItemId);
            await ctx.Pricing.UnselectLineAsync(item, line, new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.PricingRequest, item.Id,
                $"إلغاء اختيار عرض «{offer.Supplier.Name}» للبند «{requestItem.Description}» في الطلب {PricingContext.Label(item)}"));
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }
}
