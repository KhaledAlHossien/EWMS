using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Devices.Commands.Create
{
    public class CreateDeviceCommandHandler : IRequestHandler<CreateDeviceCommand, DeviceResponseDto>
    {
        private readonly IDeviceService _devices;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;

        public CreateDeviceCommandHandler(IDeviceService devices, IDeviceInventoryLogService log, ICurrentUserService currentUser)
        {
            _devices = devices;
            _log = log;
            _currentUser = currentUser;
        }

        public async Task<DeviceResponseDto> Handle(CreateDeviceCommand request, CancellationToken ct)
        {
            var device = new Device();
            DeviceSupport.Apply(request.Dto, device);
            if (await _devices.ExistsByNameAndModelAsync(device.Name, device.Model))
                throw new InvalidOperationException("الجهاز بنفس الاسم والموديل موجود في الكتالوج — اختره بدل إضافته من جديد");

            await _devices.AddAsync(device);
            await _log.AddAsync(Log(DeviceInventoryEntity.Device, device.Id, DeviceInventoryAction.Created, _currentUser.UserId, Title(device)));
            return await DtoAsync(device.Id);
        }

        private async Task<DeviceResponseDto> DtoAsync(int id) =>
            ToDto(await _devices.GetWithCountAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود"));
    }
}
