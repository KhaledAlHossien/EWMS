using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Devices.Queries.GetAll
{
    public class GetAllDevicesQueryHandler
        : IRequestHandler<GetAllDevicesQuery, List<DeviceResponseDto>>
    {
        private readonly IDeviceService _deviceService;
        private readonly IMapper _mapper;

        public GetAllDevicesQueryHandler(IDeviceService deviceService, IMapper mapper)
        {
            _deviceService = deviceService;
            _mapper = mapper;
        }

        public async Task<List<DeviceResponseDto>> Handle(
            GetAllDevicesQuery request,
            CancellationToken cancellationToken)
        {
            var devices = await _deviceService.GetAllAsync();
            return _mapper.Map<List<DeviceResponseDto>>(devices);
        }
    }
}
