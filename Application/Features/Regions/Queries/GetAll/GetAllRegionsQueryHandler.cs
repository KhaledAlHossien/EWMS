using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Regions.Queries.GetAll
{
    public class GetAllRegionsQueryHandler
        : IRequestHandler<GetAllRegionsQuery, List<RegionResponseDto>>
    {
        private readonly IRegionService _regionService;
        private readonly IMapper _mapper;

        public GetAllRegionsQueryHandler(IRegionService regionService, IMapper mapper)
        {
            _regionService = regionService;
            _mapper = mapper;
        }

        public async Task<List<RegionResponseDto>> Handle(
            GetAllRegionsQuery request,
            CancellationToken cancellationToken)
        {
            var regions = await _regionService.GetAllAsync();
            return _mapper.Map<List<RegionResponseDto>>(regions);
        }
    }
}
