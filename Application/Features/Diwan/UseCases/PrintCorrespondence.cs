using System.Globalization;
using Application.Common;
using Application.DTOs.Response;
using Application.Features.Vacations;
using Domain.Entities;
using MediatR;

namespace Application.Features.Diwan
{
    public record PrintCorrespondenceQuery(int Id) : IRequest<CorrespondencePrintDto>;

    /// <summary>
    /// بيانات طباعة الكتاب الصادر المعتمد أو الصادر: ترويسة الفرع، رقمه وتاريخه (هجري + ميلادي)، الجهة والموضوع ونص الكتاب،
    /// واسم المعتمِد مع **توقيعه الذي اعتمد به** (إصدار التوقيع المثبَّت وقت الاعتماد لا الحالي، فلا تتغير الطباعة لاحقاً).
    /// لا يُطبع إلا الصادر بعد الاعتماد، لمن يرى المعاملة.
    /// </summary>
    public class PrintCorrespondenceHandler(DiwanContext ctx) : IRequestHandler<PrintCorrespondenceQuery, CorrespondencePrintDto>
    {
        public async Task<CorrespondencePrintDto> Handle(PrintCorrespondenceQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var c = await ctx.LoadAsync(viewer, request.Id);
            if (c.Direction != CorrespondenceDirection.Outgoing || c.OutgoingState is not (OutgoingState.Approved or OutgoingState.Issued))
                throw new InvalidOperationException("لا يُطبع إلا الكتاب الصادر بعد اعتماده");

            var date = c.IssuedAt ?? c.ApprovedAt ?? c.RegisteredAt;
            return new CorrespondencePrintDto
            {
                Number = DiwanRules.Number(c.Direction, c.Year, c.Sequence),
                IsIssued = c.OutgoingState == OutgoingState.Issued,
                BranchName = c.Branch?.Name ?? string.Empty,
                DateHijri = HijriDate(date.ToLocalTime()),
                Date = date,
                ExternalNumber = c.ExternalNumber,
                ReplyToNumber = c.ReplyTo is null ? null : DiwanRules.Number(c.ReplyTo.Direction, c.ReplyTo.Year, c.ReplyTo.Sequence),
                Party = c.Party,
                Subject = c.Subject,
                Body = c.Summary,
                ApproverName = c.ApprovedByUser?.FullName,
                ApproverSignature = await ctx.Signatures.GetImageAsync(c.ApprovedSignatureId)
            };
        }

        private static string HijriDate(DateTime date)
        {
            var calendar = new UmAlQuraCalendar();
            date = date.Date.AddDays(VacationRules.HijriDayOffset);
            return $"{calendar.GetYear(date)}/{calendar.GetMonth(date)}/{calendar.GetDayOfMonth(date)}";
        }
    }
}
