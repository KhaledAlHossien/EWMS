using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Commands.Delete
{
    public class DeleteMaintenanceRequestStatusCommandHandler : IRequestHandler<DeleteMaintenanceRequestStatusCommand, Unit>
    {
        private readonly IMaintenanceRequestStatusService _service;

        public DeleteMaintenanceRequestStatusCommandHandler(IMaintenanceRequestStatusService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(DeleteMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف حالة مستخدمة في طلبات صيانة");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }

        private async Task<MaintenanceRequestStatus> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("حالة الطلب غير موجودة");
    }
}
