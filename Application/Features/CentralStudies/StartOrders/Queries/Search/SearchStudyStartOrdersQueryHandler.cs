using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Queries.Search
{
    public class SearchStudyStartOrdersQueryHandler : IRequestHandler<SearchStudyStartOrdersQuery, PagedResultDto<StudyStartOrderDto>>
    {
        private readonly IStudyStartOrderService _service;
        private readonly StudyKit _kit;
        private readonly StudyStartOrderSupport _support;

        public SearchStudyStartOrdersQueryHandler(IStudyStartOrderService service, StudyKit kit, StudyStartOrderSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<PagedResultDto<StudyStartOrderDto>> Handle(SearchStudyStartOrdersQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyStartOrderDto>
            {
                Items = items.Select(x => _support.ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }
    }
}
