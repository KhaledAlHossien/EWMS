using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Sites.Queries.GetAll
{
    public class GetAllSitesQueryHandler
        : IRequestHandler<GetAllSitesQuery, List<SiteResponseDto>>
    {
        private readonly ISiteService _siteService;
        private readonly IMapper _mapper;

        public GetAllSitesQueryHandler(ISiteService siteService, IMapper mapper)
        {
            _siteService = siteService;
            _mapper = mapper;
        }

        public async Task<List<SiteResponseDto>> Handle(
            GetAllSitesQuery request,
            CancellationToken cancellationToken)
        {
            var sites = await _siteService.GetAllAsync();
            return _mapper.Map<List<SiteResponseDto>>(sites);
        }
    }
}
