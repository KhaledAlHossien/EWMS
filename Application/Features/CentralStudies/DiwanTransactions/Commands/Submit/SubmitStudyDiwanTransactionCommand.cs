using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Submit
{
    /// <summary>إرسال المعاملة لاعتماد رئيس الفرع بعد تسجيل الرد المركزي وإرفاق صورة كتابه</summary>
    public record SubmitStudyDiwanTransactionCommand(int Id) : IRequest<StudyDiwanTransactionDto>;
}
