using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Return
{
    /// <summary>رئيس الفرع يعيد المعاملة إلى الديوان لتصحيحها أو تصحيح مرفقاتها (السبب إلزامي)</summary>
    public record ReturnStudyDiwanTransactionCommand(int Id, StudyDiwanReturnFormDto Dto) : IRequest<StudyDiwanTransactionDto>;
}
