using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Regions.Queries.GetById
{
    public class GetRegionByIdQueryHandler
        : IRequestHandler<GetRegionByIdQuery, RegionResponseDto>
    {
        private readonly IRegionService _regionService;
        private readonly IMapper _mapper;

        public GetRegionByIdQueryHandler(IRegionService regionService, IMapper mapper)
        {
            _regionService = regionService;
            _mapper = mapper;
        }

        public async Task<RegionResponseDto> Handle(
            GetRegionByIdQuery request,
            CancellationToken cancellationToken)
        {
            var region = await _regionService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المنطقة غير موجودة");

            return _mapper.Map<RegionResponseDto>(region);
        }
    }
}
