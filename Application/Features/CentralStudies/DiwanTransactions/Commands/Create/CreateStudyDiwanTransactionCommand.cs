using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Create
{
    /// <summary>تسجيل الصادر إلى الديوان المركزي لرد معتمد: ينشئ معاملة الديوان مسودةً برقم DWN</summary>
    public record CreateStudyDiwanTransactionCommand(StudyDiwanTransactionFormDto Dto) : IRequest<StudyDiwanTransactionDto>;
}
