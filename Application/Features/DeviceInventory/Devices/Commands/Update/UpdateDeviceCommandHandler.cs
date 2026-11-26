using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory.Devices.Commands.Update
{
    public class UpdateDeviceCommandHandler : IRequestHandler<UpdateDeviceCommand, DeviceResponseDto>
    {
        private readonly IDeviceService _devices;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;

        public UpdateDeviceCommandHandler(IDeviceService devices, IDeviceInventoryLogService log, ICurrentUserService currentUser)
        {
            _devices = devices;
            _log = log;
            _currentUser = currentUser;
        }

        public async Task<DeviceResponseDto> Handle(UpdateDeviceCommand request, CancellationToken ct)
        {
            var device = await _devices.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            var before = new Device { Name = device.Name, Model = device.Model, Description = device.Description, Category = device.Category, Manufacturer = device.Manufacturer };

            DeviceSupport.Apply(request.Dto, device);
            if (await _devices.ExistsByNameAndModelAsync(device.Name, device.Model, device.Id))
                throw new InvalidOperationException("يوجد جهاز آخر بنفس الاسم والموديل");

            await _devices.UpdateAsync(device, request.Dto.RowVersion);

            var diff = new Diff()
                .Add("الاسم", before.Name, device.Name)
                .Add("الموديل", before.Model, device.Model)
                .Add("الفئة", before.Category, device.Category)
                .Add("الشركة المصنّعة", before.Manufacturer, device.Manufacturer)
                .Add("الوصف", before.Description, device.Description);
            if (diff.Any)
                await _log.AddAsync(Log(DeviceInventoryEntity.Device, device.Id, DeviceInventoryAction.Updated, _currentUser.UserId, Title(device), diff.ToString()));

            return await DtoAsync(device.Id);
        }

        private async Task<DeviceResponseDto> DtoAsync(int id) =>
            ToDto(await _devices.GetWithCountAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود"));
    }
}
