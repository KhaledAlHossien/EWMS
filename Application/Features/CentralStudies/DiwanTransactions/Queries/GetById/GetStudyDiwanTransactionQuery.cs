using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Queries.GetById
{
    /// <summary>المعاملة مع السلسلة وسطر توقيع رئيس الفرع</summary>
    public record GetStudyDiwanTransactionQuery(int Id) : IRequest<StudyDiwanTransactionDto>;
}
