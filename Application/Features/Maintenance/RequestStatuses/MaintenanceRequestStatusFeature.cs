using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses
{
    public record GetAllMaintenanceRequestStatusesQuery : IRequest<List<MaintenanceRequestStatusResponseDto>>;
    public record GetMaintenanceRequestStatusByIdQuery(int Id) : IRequest<MaintenanceRequestStatusResponseDto>;
    public record CreateMaintenanceRequestStatusCommand(MaintenanceRequestStatusRequestDto Dto) : IRequest<MaintenanceRequestStatusResponseDto>;
    public record UpdateMaintenanceRequestStatusCommand(int Id, MaintenanceRequestStatusRequestDto Dto) : IRequest<MaintenanceRequestStatusResponseDto>;
    public record DeleteMaintenanceRequestStatusCommand(int Id) : IRequest<Unit>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class MaintenanceRequestStatusRequestDtoValidator : AbstractValidator<MaintenanceRequestStatusRequestDto>
    {
        public MaintenanceRequestStatusRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم الحالة مطلوب")
                .MaximumLength(100).WithMessage("اسم الحالة لا يتجاوز 100 حرف");

            RuleFor(x => x.Color)
                .NotEmpty().WithMessage("لون الحالة مطلوب")
                .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("اللون يجب أن يكون بصيغة #RRGGBB");

            RuleFor(x => x.Stage)
                .Must(s => Enum.IsDefined(typeof(MaintenanceStage), s)).WithMessage("اختر مرحلة الحالة");
        }
    }

    public class CreateMaintenanceRequestStatusCommandValidator : AbstractValidator<CreateMaintenanceRequestStatusCommand>
    {
        public CreateMaintenanceRequestStatusCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new MaintenanceRequestStatusRequestDtoValidator());
    }

    public class UpdateMaintenanceRequestStatusCommandValidator : AbstractValidator<UpdateMaintenanceRequestStatusCommand>
    {
        public UpdateMaintenanceRequestStatusCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new MaintenanceRequestStatusRequestDtoValidator());
    }

    // ════════════════════ المعالج ════════════════════

    public class MaintenanceRequestStatusHandler :
        IRequestHandler<GetAllMaintenanceRequestStatusesQuery, List<MaintenanceRequestStatusResponseDto>>,
        IRequestHandler<GetMaintenanceRequestStatusByIdQuery, MaintenanceRequestStatusResponseDto>,
        IRequestHandler<CreateMaintenanceRequestStatusCommand, MaintenanceRequestStatusResponseDto>,
        IRequestHandler<UpdateMaintenanceRequestStatusCommand, MaintenanceRequestStatusResponseDto>,
        IRequestHandler<DeleteMaintenanceRequestStatusCommand, Unit>
    {
        private readonly IMaintenanceRequestStatusService _service;
        private readonly IMapper _mapper;

        public MaintenanceRequestStatusHandler(IMaintenanceRequestStatusService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        private async Task<MaintenanceRequestStatus> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("حالة الطلب غير موجودة");

        public async Task<List<MaintenanceRequestStatusResponseDto>> Handle(GetAllMaintenanceRequestStatusesQuery request, CancellationToken ct) =>
            _mapper.Map<List<MaintenanceRequestStatusResponseDto>>(await _service.GetAllAsync());

        public async Task<MaintenanceRequestStatusResponseDto> Handle(GetMaintenanceRequestStatusByIdQuery request, CancellationToken ct) =>
            _mapper.Map<MaintenanceRequestStatusResponseDto>(await LoadAsync(request.Id));

        public async Task<MaintenanceRequestStatusResponseDto> Handle(CreateMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name))
                throw new InvalidOperationException("توجد حالة بنفس الاسم مسبقاً");

            var entity = _mapper.Map<MaintenanceRequestStatus>(request.Dto);
            entity.Name = name;
            entity.Color = request.Dto.Color.ToUpperInvariant();

            return _mapper.Map<MaintenanceRequestStatusResponseDto>(await _service.AddAsync(entity));
        }

        public async Task<MaintenanceRequestStatusResponseDto> Handle(UpdateMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            var name = request.Dto.Name.Trim();
            if (await _service.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("توجد حالة أخرى بنفس الاسم");

            _mapper.Map(request.Dto, entity);
            entity.Name = name;
            entity.Color = request.Dto.Color.ToUpperInvariant();
            await _service.UpdateAsync(entity);

            return _mapper.Map<MaintenanceRequestStatusResponseDto>(entity);
        }

        public async Task<Unit> Handle(DeleteMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var entity = await LoadAsync(request.Id);

            if (await _service.IsUsedAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف حالة مستخدمة في طلبات صيانة");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}
