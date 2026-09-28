using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Devices.Commands.Update
{
    public class UpdateDeviceCommandHandler
        : IRequestHandler<UpdateDeviceCommand, DeviceResponseDto>
    {
        private readonly IDeviceService _deviceService;
        private readonly IMapper _mapper;

        public UpdateDeviceCommandHandler(IDeviceService deviceService, IMapper mapper)
        {
            _deviceService = deviceService;
            _mapper = mapper;
        }

        public async Task<DeviceResponseDto> Handle(
            UpdateDeviceCommand request,
            CancellationToken cancellationToken)
        {
            var device = await _deviceService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الجهاز غير موجود");

            _mapper.Map(request.DeviceDto, device);
            await _deviceService.UpdateAsync(device);

            return _mapper.Map<DeviceResponseDto>(device);
        }
    }
}
