using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.DeviceSites.Queries.GetAll
{
    public class GetAllDeviceSitesQueryHandler
        : IRequestHandler<GetAllDeviceSitesQuery, List<DeviceSiteResponseDto>>
    {
        private readonly IDeviceSiteService _deviceSiteService;
        private readonly IMapper _mapper;

        public GetAllDeviceSitesQueryHandler(IDeviceSiteService deviceSiteService, IMapper mapper)
        {
            _deviceSiteService = deviceSiteService;
            _mapper = mapper;
        }

        public async Task<List<DeviceSiteResponseDto>> Handle(
            GetAllDeviceSitesQuery request,
            CancellationToken cancellationToken)
        {
            var deviceSites = request switch
            {
                { SiteId: not null } => await _deviceSiteService.GetBySiteAsync(request.SiteId.Value),
                { DeviceId: not null } => await _deviceSiteService.GetByDeviceAsync(request.DeviceId.Value),
                _ => await _deviceSiteService.GetAllAsync()
            };

            return _mapper.Map<List<DeviceSiteResponseDto>>(deviceSites);
        }
    }
}
