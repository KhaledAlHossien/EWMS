using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Commands.Delete
{
    public class DeleteDeviceMaintenanceCommandHandler : IRequestHandler<DeleteDeviceMaintenanceCommand, Unit>
    {
        private readonly IDeviceMaintenanceService _service;

        public DeleteDeviceMaintenanceCommandHandler(IDeviceMaintenanceService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(DeleteDeviceMaintenanceCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.HasRequestsAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف جهاز له طلبات صيانة — سجل إصلاحاته محفوظ");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }

        private async Task<DeviceMaintenance> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
    }
}
