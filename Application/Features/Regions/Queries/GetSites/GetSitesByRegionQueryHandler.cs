using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Regions.Queries.GetSites
{
    public class GetSitesByRegionQueryHandler
        : IRequestHandler<GetSitesByRegionQuery, List<SiteResponseDto>>
    {
        private readonly IRegionService _regionService;
        private readonly ISiteService _siteService;
        private readonly IMapper _mapper;

        public GetSitesByRegionQueryHandler(
            IRegionService regionService,
            ISiteService siteService,
            IMapper mapper)
        {
            _regionService = regionService;
            _siteService = siteService;
            _mapper = mapper;
        }

        public async Task<List<SiteResponseDto>> Handle(
            GetSitesByRegionQuery request,
            CancellationToken cancellationToken)
        {
            if (!await _regionService.ExistsAsync(request.RegionId))
                throw new KeyNotFoundException("المنطقة غير موجودة");

            var sites = await _siteService.GetByRegionAsync(request.RegionId);
            return _mapper.Map<List<SiteResponseDto>>(sites);
        }
    }
}
