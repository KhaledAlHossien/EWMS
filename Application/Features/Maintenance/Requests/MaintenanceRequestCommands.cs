using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.Requests
{
    public record CreateMaintenanceRequestCommand(SaveMaintenanceRequestDto Dto) : IRequest<MaintenanceRequestResponseDto>;
    public record UpdateMaintenanceRequestCommand(int Id, SaveMaintenanceRequestDto Dto) : IRequest<MaintenanceRequestResponseDto>;
    public record DeleteMaintenanceRequestCommand(int Id) : IRequest<Unit>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class SaveMaintenanceRequestDtoValidator : AbstractValidator<SaveMaintenanceRequestDto>
    {
        public SaveMaintenanceRequestDtoValidator()
        {
            RuleFor(x => x.ClientName)
                .NotEmpty().WithMessage("اسم العميل مطلوب")
                .MaximumLength(200).WithMessage("اسم العميل لا يتجاوز 200 حرف");

            RuleFor(x => x.ClientPhone)
                .MaximumLength(30).WithMessage("رقم الهاتف لا يتجاوز 30 خانة")
                .Matches(@"^[0-9+\-\s()]*$").WithMessage("رقم الهاتف يحتوي على رموز غير صحيحة");

            RuleFor(x => x.DeviceTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع الجهاز");
            RuleFor(x => x.DamageTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع العطل");
            RuleFor(x => x.DeviceCompanyId).GreaterThan(0).WithMessage("يجب اختيار الشركة المصنعة");
            RuleFor(x => x.MaintenanceRequestStatusId).GreaterThan(0).WithMessage("يجب اختيار حالة الطلب");

            RuleFor(x => x.Model).MaximumLength(100).WithMessage("الموديل لا يتجاوز 100 حرف");
            RuleFor(x => x.SerialNumber).MaximumLength(100).WithMessage("الرقم التسلسلي لا يتجاوز 100 حرف");
            RuleFor(x => x.Accessories).MaximumLength(500).WithMessage("الملحقات لا تتجاوز 500 حرف");
            RuleFor(x => x.Description).MaximumLength(2000).WithMessage("الوصف لا يتجاوز 2000 حرف");

            RuleFor(x => x.CompletedAt)
                .GreaterThanOrEqualTo(x => x.StartedAt!.Value)
                .When(x => x.StartedAt != null && x.CompletedAt != null)
                .WithMessage("تاريخ الإنجاز لا يمكن أن يسبق تاريخ البدء");
        }
    }

    public class CreateMaintenanceRequestCommandValidator : AbstractValidator<CreateMaintenanceRequestCommand>
    {
        public CreateMaintenanceRequestCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new SaveMaintenanceRequestDtoValidator());
    }

    public class UpdateMaintenanceRequestCommandValidator : AbstractValidator<UpdateMaintenanceRequestCommand>
    {
        public UpdateMaintenanceRequestCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new SaveMaintenanceRequestDtoValidator());
    }

    // ════════════════════ المعالج ════════════════════

    public class MaintenanceRequestCommandsHandler :
        IRequestHandler<CreateMaintenanceRequestCommand, MaintenanceRequestResponseDto>,
        IRequestHandler<UpdateMaintenanceRequestCommand, MaintenanceRequestResponseDto>,
        IRequestHandler<DeleteMaintenanceRequestCommand, Unit>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IDeviceTypeService _deviceTypeService;
        private readonly IDamageTypeService _damageTypeService;
        private readonly IDeviceCompanyService _companyService;
        private readonly IMaintenanceRequestStatusService _statusService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public MaintenanceRequestCommandsHandler(
            IMaintenanceRequestService requestService,
            IDeviceTypeService deviceTypeService,
            IDamageTypeService damageTypeService,
            IDeviceCompanyService companyService,
            IMaintenanceRequestStatusService statusService,
            IUserService userService,
            IMapper mapper)
        {
            _requestService = requestService;
            _deviceTypeService = deviceTypeService;
            _damageTypeService = damageTypeService;
            _companyService = companyService;
            _statusService = statusService;
            _userService = userService;
            _mapper = mapper;
        }

        private async Task<MaintenanceRequest> LoadAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

        private async Task EnsureReferencesAsync(SaveMaintenanceRequestDto dto)
        {
            if (!await _deviceTypeService.ExistsAsync(dto.DeviceTypeId))
                throw new KeyNotFoundException("نوع الجهاز المحدد غير موجود");
            if (!await _damageTypeService.ExistsAsync(dto.DamageTypeId))
                throw new KeyNotFoundException("نوع العطل المحدد غير موجود");
            if (!await _companyService.ExistsAsync(dto.DeviceCompanyId))
                throw new KeyNotFoundException("الشركة المصنعة المحددة غير موجودة");
            if (!await _statusService.ExistsAsync(dto.MaintenanceRequestStatusId))
                throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");
        }

        // حقول البحث تُحفظ بلا فراغات زائدة حتى يعمل "يبدأ بـ" بشكل صحيح
        private static void Trim(MaintenanceRequest r)
        {
            r.ClientName = r.ClientName.Trim();
            r.ClientPhone = r.ClientPhone.Trim();
            r.Model = r.Model.Trim();
            r.SerialNumber = r.SerialNumber.Trim();
            r.Accessories = r.Accessories.Trim();
            r.Description = r.Description.Trim();
        }

        public async Task<MaintenanceRequestResponseDto> Handle(CreateMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            await EnsureReferencesAsync(request.Dto);

            var entity = _mapper.Map<MaintenanceRequest>(request.Dto);
            Trim(entity);

            // الفني = من سجّل الطلب، والقسم يُحفظ لحظة التسجيل ليراه رئيس القسم
            entity.UserId = user.Id;
            entity.DepartmentId = user.DepartmentId;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;

            var created = await _requestService.AddAsync(entity);
            return _mapper.Map<MaintenanceRequestResponseDto>(await LoadAsync(created.Id));
        }

        public async Task<MaintenanceRequestResponseDto> Handle(UpdateMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.EnsureCanAccess(user, entity.UserId, entity.DepartmentId,
                "لا يمكنك تعديل طلب صيانة خارج نطاقك");

            await EnsureReferencesAsync(request.Dto);

            // الفني والقسم وتاريخ الإنشاء لا تتغير (ليست في الـ DTO)
            _mapper.Map(request.Dto, entity);
            Trim(entity);
            entity.UpdatedAt = DateTime.UtcNow;

            await _requestService.UpdateAsync(entity);
            return _mapper.Map<MaintenanceRequestResponseDto>(await LoadAsync(entity.Id));
        }

        public async Task<Unit> Handle(DeleteMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.EnsureCanAccess(user, entity.UserId, entity.DepartmentId,
                "لا يمكنك حذف طلب صيانة خارج نطاقك");

            await _requestService.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}
