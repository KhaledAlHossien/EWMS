using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Refer
{
    /// <summary>رئيس الفرع يقرأ كتاب الرد المركزي ويحيل المعاملة إلى مدير المشاريع بموافقة (← أمر البدء) أو برفض (← اختيار التعديل)</summary>
    public record ReferStudyDiwanTransactionCommand(int Id, StudyDiwanReferFormDto Dto) : IRequest<StudyDiwanTransactionDto>;
}
