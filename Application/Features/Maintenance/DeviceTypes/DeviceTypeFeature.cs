using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes
{
    public record GetAllDeviceTypesQuery : IRequest<List<DeviceTypeResponseDto>>;
    public record GetDeviceTypeByIdQuery(int Id) : IRequest<DeviceTypeResponseDto>;
    public record CreateDeviceTypeCommand(DeviceTypeRequestDto Dto) : IRequest<DeviceTypeResponseDto>;
    public record UpdateDeviceTypeCommand(int Id, DeviceTypeRequestDto Dto) : IRequest<DeviceTypeResponseDto>;
    public record DeleteDeviceTypeCommand(int Id) : IRequest<Unit>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class DeviceTypeRequestDtoValidator : AbstractValidator<DeviceTypeRequestDto>
    {
        public DeviceTypeRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم نوع الجهاز مطلوب")
                .MaximumLength(100).WithMessage("اسم نوع الجهاز لا يتجاوز 100 حرف");
        }
    }

    public class CreateDeviceTypeCommandValidator : AbstractValidator<CreateDeviceTypeCommand>
    {
        public CreateDeviceTypeCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceTypeRequestDtoValidator());
    }

    public class UpdateDeviceTypeCommandValidator : AbstractValidator<UpdateDeviceTypeCommand>
    {
        public UpdateDeviceTypeCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceTypeRequestDtoValidator());
    }

    // ════════════════════ المعالج ════════════════════

    public class DeviceTypeHandler :
        IRequestHandler<GetAllDeviceTypesQuery, List<DeviceTypeResponseDto>>,
        IRequestHandler<GetDeviceTypeByIdQuery, DeviceTypeResponseDto>,
        IRequestHandler<CreateDeviceTypeCommand, DeviceTypeResponseDto>,
        IRequestHandler<UpdateDeviceTypeCommand, DeviceTypeResponseDto>,
        IRequestHandler<DeleteDeviceTypeCommand, Unit>
    {
        private readonly IDeviceTypeService _service;
        private readonly IMapper _mapper;

        public DeviceTypeHandler(IDeviceTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        private async Task<DeviceType> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("نوع الجهاز غير موجود");

        public async Task<List<DeviceTypeResponseDto>> Handle(GetAllDeviceTypesQuery request, CancellationToken ct) =>
            _mapper.Map<List<DeviceTypeResponseDto>>(await _service.GetAllAsync());

        public async Task<DeviceTypeResponseDto> Handle(GetDeviceTypeByIdQuery request, CancellationToken ct) =>
            _mapper.Map<DeviceTypeResponseDto>(await LoadAsync(request.Id));

        public async Task<DeviceTypeResponseDto> Handle(CreateDeviceTypeCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name))
                throw new InvalidOperationException("يوجد نوع جهاز بنفس الاسم مسبقاً");

            var entity = _mapper.Map<DeviceType>(request.Dto);
            entity.Name = name;

            return _mapper.Map<DeviceTypeResponseDto>(await _service.AddAsync(entity));
        }

        public async Task<DeviceTypeResponseDto> Handle(UpdateDeviceTypeCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("يوجد نوع جهاز آخر بنفس الاسم");

            _mapper.Map(request.Dto, entity);
            entity.Name = name;
            await _service.UpdateAsync(entity);

            return _mapper.Map<DeviceTypeResponseDto>(entity);
        }

        public async Task<Unit> Handle(DeleteDeviceTypeCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف نوع جهاز مستخدم في أجهزة الصيانة");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}
