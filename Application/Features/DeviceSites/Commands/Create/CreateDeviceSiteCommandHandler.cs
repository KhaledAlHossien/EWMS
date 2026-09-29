using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using DeviceSiteEntity = Domain.Entities.DeviceSite;

namespace Application.Features.DeviceSites.Commands.Create
{
    public class CreateDeviceSiteCommandHandler
        : IRequestHandler<CreateDeviceSiteCommand, DeviceSiteResponseDto>
    {
        private readonly IDeviceSiteService _deviceSiteService;
        private readonly IDeviceService _deviceService;
        private readonly ISiteService _siteService;
        private readonly IMapper _mapper;

        public CreateDeviceSiteCommandHandler(
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
            CreateDeviceSiteCommand request,
            CancellationToken cancellationToken)
        {
            if (!await _deviceService.ExistsAsync(request.DeviceSiteDto.DeviceId))
                throw new KeyNotFoundException("الجهاز المحدد غير موجود");

            if (!await _siteService.ExistsAsync(request.DeviceSiteDto.SiteId))
                throw new KeyNotFoundException("الموقع المحدد غير موجود");

            // الجهاز نوع/موديل قابل للتكرار: يمكن تركيب نفس الجهاز أكثر من مرة في نفس الموقع
            // (IP ومعلومات مختلفة لكل تركيب) — قرار المستخدم 2026-09-29

            var deviceSite = _mapper.Map<DeviceSiteEntity>(request.DeviceSiteDto);
            var created = await _deviceSiteService.AddAsync(deviceSite);

            var withDetails = await _deviceSiteService.GetByIdAsync(created.Id) ?? created;
            return _mapper.Map<DeviceSiteResponseDto>(withDetails);
        }
    }
}
