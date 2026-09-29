using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies
{
    public record GetAllDeviceCompaniesQuery : IRequest<List<DeviceCompanyResponseDto>>;
    public record GetDeviceCompanyByIdQuery(int Id) : IRequest<DeviceCompanyResponseDto>;
    public record CreateDeviceCompanyCommand(DeviceCompanyRequestDto Dto) : IRequest<DeviceCompanyResponseDto>;
    public record UpdateDeviceCompanyCommand(int Id, DeviceCompanyRequestDto Dto) : IRequest<DeviceCompanyResponseDto>;
    public record DeleteDeviceCompanyCommand(int Id) : IRequest<Unit>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class DeviceCompanyRequestDtoValidator : AbstractValidator<DeviceCompanyRequestDto>
    {
        public DeviceCompanyRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم الشركة المصنعة مطلوب")
                .MaximumLength(100).WithMessage("اسم الشركة لا يتجاوز 100 حرف");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
        }
    }

    public class CreateDeviceCompanyCommandValidator : AbstractValidator<CreateDeviceCompanyCommand>
    {
        public CreateDeviceCompanyCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceCompanyRequestDtoValidator());
    }

    public class UpdateDeviceCompanyCommandValidator : AbstractValidator<UpdateDeviceCompanyCommand>
    {
        public UpdateDeviceCompanyCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceCompanyRequestDtoValidator());
    }

    // ════════════════════ المعالج ════════════════════

    public class DeviceCompanyHandler :
        IRequestHandler<GetAllDeviceCompaniesQuery, List<DeviceCompanyResponseDto>>,
        IRequestHandler<GetDeviceCompanyByIdQuery, DeviceCompanyResponseDto>,
        IRequestHandler<CreateDeviceCompanyCommand, DeviceCompanyResponseDto>,
        IRequestHandler<UpdateDeviceCompanyCommand, DeviceCompanyResponseDto>,
        IRequestHandler<DeleteDeviceCompanyCommand, Unit>
    {
        private readonly IDeviceCompanyService _service;
        private readonly IMapper _mapper;

        public DeviceCompanyHandler(IDeviceCompanyService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        private async Task<DeviceCompany> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الشركة المصنعة غير موجودة");

        public async Task<List<DeviceCompanyResponseDto>> Handle(GetAllDeviceCompaniesQuery request, CancellationToken ct) =>
            _mapper.Map<List<DeviceCompanyResponseDto>>(await _service.GetAllAsync());

        public async Task<DeviceCompanyResponseDto> Handle(GetDeviceCompanyByIdQuery request, CancellationToken ct) =>
            _mapper.Map<DeviceCompanyResponseDto>(await LoadAsync(request.Id));

        public async Task<DeviceCompanyResponseDto> Handle(CreateDeviceCompanyCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name))
                throw new InvalidOperationException("توجد شركة بنفس الاسم مسبقاً");

            var entity = _mapper.Map<DeviceCompany>(request.Dto);
            entity.Name = name;

            return _mapper.Map<DeviceCompanyResponseDto>(await _service.AddAsync(entity));
        }

        public async Task<DeviceCompanyResponseDto> Handle(UpdateDeviceCompanyCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("توجد شركة أخرى بنفس الاسم");

            _mapper.Map(request.Dto, entity);
            entity.Name = name;
            await _service.UpdateAsync(entity);

            return _mapper.Map<DeviceCompanyResponseDto>(entity);
        }

        public async Task<Unit> Handle(DeleteDeviceCompanyCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف شركة مستخدمة في طلبات صيانة");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}
