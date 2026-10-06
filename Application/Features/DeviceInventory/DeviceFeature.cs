using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.DeviceInventory.DeviceInventoryRules;

namespace Application.Features.DeviceInventory
{
    // كتالوج الأجهزة: نوع/موديل قابل للتركيب عدة مرات — الاسم + الموديل فريدان معاً (مراجعة 2026-10-05)
    public record GetAllDevicesQuery : IRequest<List<DeviceResponseDto>>;
    public record GetDeviceByIdQuery(int Id) : IRequest<DeviceResponseDto>;
    public record CreateDeviceCommand(DeviceRequestDto Dto) : IRequest<DeviceResponseDto>;
    public record UpdateDeviceCommand(int Id, DeviceRequestDto Dto) : IRequest<DeviceResponseDto>;
    public record DeleteDeviceCommand(int Id) : IRequest<Unit>;

    public class CreateDeviceCommandValidator : AbstractValidator<CreateDeviceCommand>
    {
        public CreateDeviceCommandValidator() => RuleFor(x => x.Dto).SetValidator(new DeviceRequestValidator());
    }

    public class UpdateDeviceCommandValidator : AbstractValidator<UpdateDeviceCommand>
    {
        public UpdateDeviceCommandValidator() => RuleFor(x => x.Dto).SetValidator(new DeviceRequestValidator());
    }

    public class DeviceHandler :
        IRequestHandler<GetAllDevicesQuery, List<DeviceResponseDto>>,
        IRequestHandler<GetDeviceByIdQuery, DeviceResponseDto>,
        IRequestHandler<CreateDeviceCommand, DeviceResponseDto>,
        IRequestHandler<UpdateDeviceCommand, DeviceResponseDto>,
        IRequestHandler<DeleteDeviceCommand, Unit>
    {
        private readonly IDeviceService _devices;
        private readonly IDeviceInventoryLogService _log;
        private readonly ICurrentUserService _currentUser;

        public DeviceHandler(IDeviceService devices, IDeviceInventoryLogService log, ICurrentUserService currentUser)
        {
            _devices = devices;
            _log = log;
            _currentUser = currentUser;
        }

        private async Task<DeviceResponseDto> DtoAsync(int id) =>
            ToDto(await _devices.GetWithCountAsync(id) ?? throw new KeyNotFoundException("الجهاز غير موجود"));

        public async Task<List<DeviceResponseDto>> Handle(GetAllDevicesQuery request, CancellationToken ct) =>
            (await _devices.GetAllWithCountsAsync()).Select(ToDto).ToList();

        public Task<DeviceResponseDto> Handle(GetDeviceByIdQuery request, CancellationToken ct) => DtoAsync(request.Id);

        private static void Apply(DeviceRequestDto dto, Device device)
        {
            device.Name = Clean(dto.Name);
            device.Model = Clean(dto.Model);
            device.Description = Clean(dto.Description);
            device.Category = Clean(dto.Category);
            device.Manufacturer = Clean(dto.Manufacturer);
        }

        public async Task<DeviceResponseDto> Handle(CreateDeviceCommand request, CancellationToken ct)
        {
            var device = new Device();
            Apply(request.Dto, device);
            if (await _devices.ExistsByNameAndModelAsync(device.Name, device.Model))
                throw new InvalidOperationException("الجهاز بنفس الاسم والموديل موجود في الكتالوج — اختره بدل إضافته من جديد");

            await _devices.AddAsync(device);
            await _log.AddAsync(Log(DeviceInventoryEntity.Device, device.Id, DeviceInventoryAction.Created, _currentUser.UserId, Title(device)));
            return await DtoAsync(device.Id);
        }

        public async Task<DeviceResponseDto> Handle(UpdateDeviceCommand request, CancellationToken ct)
        {
            var device = await _devices.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("الجهاز غير موجود");
            var before = new Device { Name = device.Name, Model = device.Model, Description = device.Description, Category = device.Category, Manufacturer = device.Manufacturer };

            Apply(request.Dto, device);
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
