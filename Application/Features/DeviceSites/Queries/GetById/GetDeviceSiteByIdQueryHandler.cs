using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.DeviceSites.Queries.GetById
{
    public class GetDeviceSiteByIdQueryHandler
        : IRequestHandler<GetDeviceSiteByIdQuery, DeviceSiteResponseDto>
    {
        private readonly IDeviceSiteService _deviceSiteService;
        private readonly IMapper _mapper;

        public GetDeviceSiteByIdQueryHandler(IDeviceSiteService deviceSiteService, IMapper mapper)
        {
            _deviceSiteService = deviceSiteService;
            _mapper = mapper;
        }

        public async Task<DeviceSiteResponseDto> Handle(
            GetDeviceSiteByIdQuery request,
            CancellationToken cancellationToken)
        {
            var deviceSite = await _deviceSiteService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الربط غير موجود");

            return _mapper.Map<DeviceSiteResponseDto>(deviceSite);
        }
    }
}
