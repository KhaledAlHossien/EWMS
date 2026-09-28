using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Devices.Queries.GetBySite
{
    public class GetDevicesBySiteQueryHandler
        : IRequestHandler<GetDevicesBySiteQuery, List<DeviceResponseDto>>
    {
        private readonly ISiteService _siteService;
        private readonly IDeviceSiteService _deviceSiteService;
        private readonly IMapper _mapper;

        public GetDevicesBySiteQueryHandler(
            ISiteService siteService,
            IDeviceSiteService deviceSiteService,
            IMapper mapper)
        {
            _siteService = siteService;
            _deviceSiteService = deviceSiteService;
            _mapper = mapper;
        }

        public async Task<List<DeviceResponseDto>> Handle(
            GetDevicesBySiteQuery request,
            CancellationToken cancellationToken)
        {
            if (!await _siteService.ExistsAsync(request.SiteId))
                throw new KeyNotFoundException("الموقع غير موجود");

            var links = await _deviceSiteService.GetBySiteAsync(request.SiteId);
            var devices = links.Select(l => l.Device).ToList();

            return _mapper.Map<List<DeviceResponseDto>>(devices);
        }
    }
}
