using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Queries.GetById
{
    public class GetStudyDiwanTransactionQueryHandler : IRequestHandler<GetStudyDiwanTransactionQuery, StudyDiwanTransactionDto>
    {
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public GetStudyDiwanTransactionQueryHandler(StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDiwanTransactionDto> Handle(GetStudyDiwanTransactionQuery request, CancellationToken ct) =>
            await _support.DetailAsync(request.Id, await _kit.ViewerAsync());
    }
}
