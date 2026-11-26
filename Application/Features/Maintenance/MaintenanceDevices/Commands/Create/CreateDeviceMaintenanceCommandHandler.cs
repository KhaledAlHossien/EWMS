using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Commands.Create
{
    public class CreateDeviceMaintenanceCommandHandler : IRequestHandler<CreateDeviceMaintenanceCommand, DeviceMaintenanceResponseDto>
    {
        private readonly IDeviceMaintenanceService _service;
        private readonly IMapper _mapper;
        private readonly DeviceMaintenanceSupport _support;

        public CreateDeviceMaintenanceCommandHandler(IDeviceMaintenanceService service, IMapper mapper, DeviceMaintenanceSupport support)
        {
            _service = service;
            _mapper = mapper;
            _support = support;
        }

        public async Task<DeviceMaintenanceResponseDto> Handle(CreateDeviceMaintenanceCommand request, CancellationToken ct)
        {
            await _support.EnsureReferencesAsync(request.Dto);

            var entity = _mapper.Map<DeviceMaintenance>(request.Dto);
            DeviceMaintenanceSupport.Trim(entity);

            if (await _service.ExistsBySerialAsync(entity.SerialNumber))
                throw new InvalidOperationException("يوجد جهاز بنفس الرقم التسلسلي مسبقاً — اختره بدل إضافته من جديد");

            var created = await _service.AddAsync(entity);
            return _mapper.Map<DeviceMaintenanceResponseDto>(await LoadAsync(created.Id));
        }

        private async Task<DeviceMaintenance> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
    }
}
