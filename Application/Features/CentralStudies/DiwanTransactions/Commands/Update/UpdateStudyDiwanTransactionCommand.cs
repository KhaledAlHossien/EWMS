using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Update
{
    /// <summary>تعديل الصادر والرد المركزي (المسودة أو المرفوضة، والرد معتمد) — يعيدها مسودة</summary>
    public record UpdateStudyDiwanTransactionCommand(int Id, StudyDiwanTransactionFormDto Dto) : IRequest<StudyDiwanTransactionDto>;
}
