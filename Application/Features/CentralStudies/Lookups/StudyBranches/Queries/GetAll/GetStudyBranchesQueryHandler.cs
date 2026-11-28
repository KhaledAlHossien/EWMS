using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Queries.GetAll
{
    public class GetStudyBranchesQueryHandler : IRequestHandler<GetStudyBranchesQuery, List<StudyBranchDto>>
    {
        private readonly IStudyLookupService _service;
        private readonly StudyKit _kit;

        public GetStudyBranchesQueryHandler(IStudyLookupService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        public async Task<List<StudyBranchDto>> Handle(GetStudyBranchesQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var items = await _service.GetStudyBranchesAsync(StudyRules.BranchLimit(viewer), request.BranchId, request.ActiveOnly);
            var used = await _service.StudyBranchesInUseAsync(items.Select(x => x.Id));
            return items.Select(x =>
            {
                var dto = _kit.Mapper.Map<StudyBranchDto>(x);
                dto.InUse = used.Contains(x.Id);
                return dto;
            }).ToList();
        }
    }
}
