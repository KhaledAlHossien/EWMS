using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Devices.Commands.Delete
{
    public class DeleteDeviceCommandHandler : IRequestHandler<DeleteDeviceCommand, Unit>
    {
        private readonly IDeviceService _devices;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;

        public DeleteDeviceCommandHandler(IDeviceService devices, IDeviceInventoryLogService log, ICurrentUserService currentUser)
        {
            _devices = devices;
            _log = log;
            _currentUser = currentUser;
        }

        public async Task<Unit> Handle(DeleteDeviceCommand request, CancellationToken ct)
        {
            var device = await _devices.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            if (await _devices.HasSiteLinksAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف جهاز له تركيبات — سجلها محفوظ");

            await _devices.DeleteAsync(device);
            await _log.AddAsync(Log(DeviceInventoryEntity.Device, device.Id, DeviceInventoryAction.Deleted, _currentUser.UserId, Title(device)));
            return Unit.Value;
        }
    }
}
