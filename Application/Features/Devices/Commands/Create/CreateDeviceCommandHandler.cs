using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;
using DeviceEntity = Domain.Entities.Device;

namespace Application.Features.Devices.Commands.Create
{
    public class CreateDeviceCommandHandler
        : IRequestHandler<CreateDeviceCommand, DeviceResponseDto>
    {
        private readonly IDeviceService _deviceService;
        private readonly IMapper _mapper;

        public CreateDeviceCommandHandler(IDeviceService deviceService, IMapper mapper)
        {
            _deviceService = deviceService;
            _mapper = mapper;
        }

        public async Task<DeviceResponseDto> Handle(
            CreateDeviceCommand request,
            CancellationToken cancellationToken)
        {
            var device = _mapper.Map<DeviceEntity>(request.DeviceDto);
            var created = await _deviceService.AddAsync(device);

            return _mapper.Map<DeviceResponseDto>(created);
        }
    }
}
