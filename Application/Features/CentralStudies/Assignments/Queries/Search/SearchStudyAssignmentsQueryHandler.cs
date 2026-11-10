using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Queries.Search
{
    public class SearchStudyAssignmentsQueryHandler : IRequestHandler<SearchStudyAssignmentsQuery, PagedResultDto<StudyAssignmentDto>>
    {
        private readonly IStudyAssignmentService _service;
        private readonly StudyKit _kit;
        private readonly StudyAssignmentSupport _support;

        public SearchStudyAssignmentsQueryHandler(IStudyAssignmentService service, StudyKit kit, StudyAssignmentSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<PagedResultDto<StudyAssignmentDto>> Handle(SearchStudyAssignmentsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (page, size) = StudyRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);
            var (items, total) = await _service.SearchAsync(request.Filter, StudyRules.BranchLimit(viewer), page, size);
            return new PagedResultDto<StudyAssignmentDto>
            {
                Items = items.Select(x => _support.ToDto(x, viewer)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = size
            };
        }
    }
}
