using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Queries.Search
{
    public class SearchStudyResponsesQueryHandler : IRequestHandler<SearchStudyResponsesQuery, PagedResultDto<StudyResponseDto>>
    {
        private readonly IStudyResponseService _service;
        private readonly StudyKit _kit;
        private readonly StudyResponseSupport _support;

        public SearchStudyResponsesQueryHandler(IStudyResponseService service, StudyKit kit, StudyResponseSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<PagedResultDto<StudyResponseDto>> Handle(SearchStudyResponsesQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyResponseDto>
            {
                Items = items.Select(x => _support.ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }
    }
}
