using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Contracts.ContractSupport;

namespace Application.Features.Contracts
{
    // عروض المناقصة أو عروض الأسعار والإرساء على أحدها بالقرار الرسمي (يحدّد المتعاقد معه). سجلات تُضاف ولا تُعدَّل.

    public record AddContractOfferCommand(int ContractId, ContractOfferRequestDto Dto) : IRequest<ContractDetailDto>;
    public record AwardContractOfferCommand(int OfferId, AwardOfferRequestDto Dto) : IRequest<ContractDetailDto>;

    public class AddContractOfferHandler(ContractContext ctx) : IRequestHandler<AddContractOfferCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(AddContractOfferCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var contract = await ctx.LoadAsync(viewer, request.ContractId);
            var contractor = await ctx.Contracts.GetContractorAsync(dto.ContractorId) ?? throw new KeyNotFoundException("المقاول غير موجود");
            ContractRules.EnsureCanAddOffer(viewer, contract, contractor, dto.Amount);
            if (dto.ReceivedAt.Date > DateTime.Today) throw new InvalidOperationException("تاريخ استلام العرض لا يكون في المستقبل");
            if ((dto.Note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");

            await ctx.Contracts.AddOfferAsync(contract, new ContractOffer
            {
                ContractId = contract.Id, ContractorId = contractor.Id, Amount = dto.Amount, ReceivedAt = dto.ReceivedAt.Date, Note = (dto.Note ?? string.Empty).Trim(),
                RecordedByUserId = viewer.Id, RecordedAt = DateTime.UtcNow
            }, new AuditEntry(viewer.Id, AuditActions.Recorded, AuditEntities.Contract, contract.Id,
                $"تسجيل عرض «{contractor.Name}» على العقد {ContractContext.Label(contract)}", $"المبلغ: {Money(dto.Amount, contract.Currency)}"));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    /// <summary>
    /// الإرساء: العرض المُرسًّى عليه يحدّد المتعاقد معه، وتاريخ الإرساء (اليوم إن لم يُحدَّد) وملاحظته يُسجَّلان على العقد.
    /// إن لم تكن للعقد قيمة بعد، تُنسخ قيمة العرض كما هي (يعدّلها مكتب العقود عند إبرام العقد).
    /// </summary>
    public class AwardContractOfferHandler(ContractContext ctx) : IRequestHandler<AwardContractOfferCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(AwardContractOfferCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (contract, offer) = await ctx.LoadOfferAsync(viewer, request.OfferId);
            ContractRules.EnsureCanAward(viewer, contract, offer);

            var awardedAt = (dto.AwardedAt ?? DateTime.Today).Date;
            if (awardedAt > DateTime.Today) throw new InvalidOperationException("تاريخ الإرساء لا يكون في المستقبل");
            if (contract.TenderOpenedAt is DateTime opened && awardedAt < opened.Date) throw new InvalidOperationException("الإرساء لا يسبق فض المناقصة");
            if ((dto.Note ?? string.Empty).Length > 1000) throw new InvalidOperationException("ملاحظة الإرساء لا تتجاوز 1000 حرف");

            var copiedValue = contract.Value == null;
            offer.IsAwarded = true;
            contract.ContractorId = offer.ContractorId;
            contract.AwardedAt = awardedAt;
            contract.AwardNote = (dto.Note ?? string.Empty).Trim();
            if (copiedValue) contract.Value = offer.Amount;

            await ctx.Contracts.UpdateAsync(contract, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Awarded, AuditEntities.Contract, contract.Id,
                    $"الإرساء على «{offer.Contractor.Name}» في العقد {ContractContext.Label(contract)}",
                    Lines($"مبلغ العرض: {Money(offer.Amount, contract.Currency)}", copiedValue ? "نُسخت قيمة العرض إلى القيمة التعاقدية" : null,
                        string.IsNullOrWhiteSpace(dto.Note) ? null : $"الملاحظة: {dto.Note.Trim()}")));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }
}
