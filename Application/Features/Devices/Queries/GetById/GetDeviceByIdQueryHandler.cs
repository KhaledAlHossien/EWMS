using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Devices.Queries.GetById
{
    public class GetDeviceByIdQueryHandler
        : IRequestHandler<GetDeviceByIdQuery, DeviceResponseDto>
    {
        private readonly IDeviceService _deviceService;
        private readonly IMapper _mapper;

        public GetDeviceByIdQueryHandler(IDeviceService deviceService, IMapper mapper)
        {
            _deviceService = deviceService;
            _mapper = mapper;
        }

        public async Task<DeviceResponseDto> Handle(
            GetDeviceByIdQuery request,
            CancellationToken cancellationToken)
        {
            var device = await _deviceService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الجهاز غير موجود");

            return _mapper.Map<DeviceResponseDto>(device);
        }
    }
}
