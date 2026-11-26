using Application.Interfaces;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Commands.Delete
{
    public class DeleteDeviceTypeCommandHandler : IRequestHandler<DeleteDeviceTypeCommand, Unit>
    {
        private readonly IDeviceTypeService _service;

        public DeleteDeviceTypeCommandHandler(IDeviceTypeService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(DeleteDeviceTypeCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف نوع جهاز مستخدم في أجهزة الصيانة");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }

        private async Task<DeviceType> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("نوع الجهاز غير موجود");
    }
}
