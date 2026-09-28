using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.DeviceSites.Commands.Update
{
    public class UpdateDeviceSiteCommandHandler
        : IRequestHandler<UpdateDeviceSiteCommand, DeviceSiteResponseDto>
    {
        private readonly IDeviceSiteService _deviceSiteService;
        private readonly IDeviceService _deviceService;
        private readonly ISiteService _siteService;
        private readonly IMapper _mapper;

        public UpdateDeviceSiteCommandHandler(
            IDeviceSiteService deviceSiteService,
            IDeviceService deviceService,
            ISiteService siteService,
            IMapper mapper)
        {
            _deviceSiteService = deviceSiteService;
            _deviceService = deviceService;
            _siteService = siteService;
            _mapper = mapper;
        }

        public async Task<DeviceSiteResponseDto> Handle(
            UpdateDeviceSiteCommand request,
            CancellationToken cancellationToken)
        {
            var deviceSite = await _deviceSiteService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الربط غير موجود");

            if (!await _deviceService.ExistsAsync(request.DeviceSiteDto.DeviceId))
                throw new KeyNotFoundException("الجهاز المحدد غير موجود");

            if (!await _siteService.ExistsAsync(request.DeviceSiteDto.SiteId))
                throw new KeyNotFoundException("الموقع المحدد غير موجود");

            if (await _deviceSiteService.ExistsLinkAsync(
                    request.DeviceSiteDto.DeviceId, request.DeviceSiteDto.SiteId, request.Id))
                throw new InvalidOperationException("هذا الجهاز مرتبط مسبقاً بهذا الموقع");

            _mapper.Map(request.DeviceSiteDto, deviceSite);
            await _deviceSiteService.UpdateAsync(deviceSite);

            var withDetails = await _deviceSiteService.GetByIdAsync(deviceSite.Id) ?? deviceSite;
            return _mapper.Map<DeviceSiteResponseDto>(withDetails);
        }
    }
}
