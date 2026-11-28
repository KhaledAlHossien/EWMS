using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Queries.Search
{
    /// <summary>بحث: status، search (رقم المعاملة أو الصادر أو الرد المركزي أو إشعار الرد أو الكود أو الجهة)، branchId، from/to (تاريخ الصادر)</summary>
    public record SearchStudyDiwanTransactionsQuery(StudyDocumentFilterDto Filter) : IRequest<PagedResultDto<StudyDiwanTransactionDto>>;
}
