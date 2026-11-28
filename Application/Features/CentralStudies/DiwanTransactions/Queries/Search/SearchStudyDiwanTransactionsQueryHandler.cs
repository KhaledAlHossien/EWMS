using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.DiwanTransactions.Queries.Search
{
    public class SearchStudyDiwanTransactionsQueryHandler : IRequestHandler<SearchStudyDiwanTransactionsQuery, PagedResultDto<StudyDiwanTransactionDto>>
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly StudyKit _kit;
        private readonly StudyDiwanTransactionSupport _support;

        public SearchStudyDiwanTransactionsQueryHandler(IStudyDiwanTransactionService service, StudyKit kit, StudyDiwanTransactionSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<PagedResultDto<StudyDiwanTransactionDto>> Handle(SearchStudyDiwanTransactionsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyDiwanTransactionDto>
            {
                Items = items.Select(x => _support.ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }
    }
}
