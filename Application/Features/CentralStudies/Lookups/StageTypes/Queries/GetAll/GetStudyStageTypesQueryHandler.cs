using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Queries.GetAll
{
    public class GetStudyStageTypesQueryHandler : IRequestHandler<GetStudyStageTypesQuery, List<StudyStageTypeDto>>
    {
        private readonly IStudyLookupService _service;
        private readonly IMapper _mapper;

        public GetStudyStageTypesQueryHandler(IStudyLookupService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        // ----- أنواع المراحل -----
        public async Task<List<StudyStageTypeDto>> Handle(GetStudyStageTypesQuery request, CancellationToken ct) =>
            _mapper.Map<List<StudyStageTypeDto>>(await _service.GetStageTypesAsync(request.ActiveOnly));
    }
}
