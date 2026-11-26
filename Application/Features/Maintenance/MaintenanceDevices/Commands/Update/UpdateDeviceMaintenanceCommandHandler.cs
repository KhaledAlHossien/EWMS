using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Commands.Update
{
    public class UpdateDeviceMaintenanceCommandHandler : IRequestHandler<UpdateDeviceMaintenanceCommand, DeviceMaintenanceResponseDto>
    {
        private readonly IDeviceMaintenanceService _service;
        private readonly IMapper _mapper;
        private readonly DeviceMaintenanceSupport _support;

        public UpdateDeviceMaintenanceCommandHandler(IDeviceMaintenanceService service, IMapper mapper, DeviceMaintenanceSupport support)
        {
            _service = service;
            _mapper = mapper;
            _support = support;
        }

        public async Task<DeviceMaintenanceResponseDto> Handle(UpdateDeviceMaintenanceCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);
            await _support.EnsureReferencesAsync(request.Dto);

            _mapper.Map(request.Dto, entity);
            DeviceMaintenanceSupport.Trim(entity);

            if (await _service.ExistsBySerialAsync(entity.SerialNumber, request.Id))
                throw new InvalidOperationException("يوجد جهاز آخر بنفس الرقم التسلسلي");

            await _service.UpdateAsync(entity);
            return _mapper.Map<DeviceMaintenanceResponseDto>(await LoadAsync(entity.Id));
        }

        private async Task<DeviceMaintenance> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
    }
}
