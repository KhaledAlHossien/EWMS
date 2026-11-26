using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Queries.GetBySerial
{
    public class GetDeviceMaintenanceBySerialQueryHandler : IRequestHandler<GetDeviceMaintenanceBySerialQuery, DeviceMaintenanceResponseDto>
    {
        private readonly IDeviceMaintenanceService _service;
        private readonly IMapper _mapper;

        public GetDeviceMaintenanceBySerialQueryHandler(IDeviceMaintenanceService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DeviceMaintenanceResponseDto> Handle(GetDeviceMaintenanceBySerialQuery request, CancellationToken ct)
        {
            var device = await _service.GetBySerialAsync(request.SerialNumber.Trim())
                ?? throw new KeyNotFoundException("لا يوجد جهاز بهذا الرقم التسلسلي — أضفه أولاً");

            return _mapper.Map<DeviceMaintenanceResponseDto>(device);
        }
    }
}
