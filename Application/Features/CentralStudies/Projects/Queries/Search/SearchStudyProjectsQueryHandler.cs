using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Queries.Search
{
    public class SearchStudyProjectsQueryHandler : IRequestHandler<SearchStudyProjectsQuery, PagedResultDto<StudyProjectDto>>
    {
        private readonly IStudyProjectService _service;
        private readonly StudyKit _kit;

        public SearchStudyProjectsQueryHandler(IStudyProjectService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        public async Task<PagedResultDto<StudyProjectDto>> Handle(SearchStudyProjectsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            foreach (var item in items) StudyProjectSupport.ApplyFlags(item, viewer);
            return new PagedResultDto<StudyProjectDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }
    }
}
