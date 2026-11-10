using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Queries.GetAll
{
    public class GetStudyLookupsQueryHandler : IRequestHandler<GetStudyLookupsQuery, List<StudyLookupDto>>
    {
        private readonly IStudyLookupService _service;
        private readonly IMapper _mapper;

        public GetStudyLookupsQueryHandler(IStudyLookupService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        // ----- أنواع المرفقات والاختصاصات -----
        public async Task<List<StudyLookupDto>> Handle(GetStudyLookupsQuery request, CancellationToken ct) =>
            request.Kind == StudyLookupKind.AttachmentType
                ? _mapper.Map<List<StudyLookupDto>>(await _service.GetAttachmentTypesAsync(request.ActiveOnly))
                : _mapper.Map<List<StudyLookupDto>>(await _service.GetDisciplinesAsync(request.ActiveOnly));
    }
}
