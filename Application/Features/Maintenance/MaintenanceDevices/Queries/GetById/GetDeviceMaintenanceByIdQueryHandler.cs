using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Queries.GetById
{
    public class GetDeviceMaintenanceByIdQueryHandler : IRequestHandler<GetDeviceMaintenanceByIdQuery, DeviceMaintenanceResponseDto>
    {
        private readonly IDeviceMaintenanceService _service;
        private readonly IMapper _mapper;

        public GetDeviceMaintenanceByIdQueryHandler(IDeviceMaintenanceService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<DeviceMaintenanceResponseDto> Handle(GetDeviceMaintenanceByIdQuery request, CancellationToken ct) =>
            _mapper.Map<DeviceMaintenanceResponseDto>(await LoadAsync(request.Id));

        private async Task<DeviceMaintenance> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
    }
}
