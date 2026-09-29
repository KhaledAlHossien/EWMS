using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.DamageTypes
{
    public record GetAllDamageTypesQuery : IRequest<List<DamageTypeResponseDto>>;
    public record GetDamageTypeByIdQuery(int Id) : IRequest<DamageTypeResponseDto>;
    public record CreateDamageTypeCommand(DamageTypeRequestDto Dto) : IRequest<DamageTypeResponseDto>;
    public record UpdateDamageTypeCommand(int Id, DamageTypeRequestDto Dto) : IRequest<DamageTypeResponseDto>;
    public record DeleteDamageTypeCommand(int Id) : IRequest<Unit>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class DamageTypeRequestDtoValidator : AbstractValidator<DamageTypeRequestDto>
    {
        public DamageTypeRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم نوع العطل مطلوب")
                .MaximumLength(100).WithMessage("اسم نوع العطل لا يتجاوز 100 حرف");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
        }
    }

    public class CreateDamageTypeCommandValidator : AbstractValidator<CreateDamageTypeCommand>
    {
        public CreateDamageTypeCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DamageTypeRequestDtoValidator());
    }

    public class UpdateDamageTypeCommandValidator : AbstractValidator<UpdateDamageTypeCommand>
    {
        public UpdateDamageTypeCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DamageTypeRequestDtoValidator());
    }

    // ════════════════════ المعالج ════════════════════

    public class DamageTypeHandler :
        IRequestHandler<GetAllDamageTypesQuery, List<DamageTypeResponseDto>>,
        IRequestHandler<GetDamageTypeByIdQuery, DamageTypeResponseDto>,
        IRequestHandler<CreateDamageTypeCommand, DamageTypeResponseDto>,
        IRequestHandler<UpdateDamageTypeCommand, DamageTypeResponseDto>,
        IRequestHandler<DeleteDamageTypeCommand, Unit>
    {
        private readonly IDamageTypeService _service;
        private readonly IMapper _mapper;

        public DamageTypeHandler(IDamageTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        private async Task<DamageType> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("نوع العطل غير موجود");

        public async Task<List<DamageTypeResponseDto>> Handle(GetAllDamageTypesQuery request, CancellationToken ct) =>
            _mapper.Map<List<DamageTypeResponseDto>>(await _service.GetAllAsync());

        public async Task<DamageTypeResponseDto> Handle(GetDamageTypeByIdQuery request, CancellationToken ct) =>
            _mapper.Map<DamageTypeResponseDto>(await LoadAsync(request.Id));

        public async Task<DamageTypeResponseDto> Handle(CreateDamageTypeCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name))
                throw new InvalidOperationException("يوجد نوع عطل بنفس الاسم مسبقاً");

            var entity = _mapper.Map<DamageType>(request.Dto);
            entity.Name = name;

            return _mapper.Map<DamageTypeResponseDto>(await _service.AddAsync(entity));
        }

        public async Task<DamageTypeResponseDto> Handle(UpdateDamageTypeCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("يوجد نوع عطل آخر بنفس الاسم");

            _mapper.Map(request.Dto, entity);
            entity.Name = name;
            await _service.UpdateAsync(entity);

            return _mapper.Map<DamageTypeResponseDto>(entity);
        }

        public async Task<Unit> Handle(DeleteDamageTypeCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف نوع عطل مستخدم في طلبات صيانة");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}
