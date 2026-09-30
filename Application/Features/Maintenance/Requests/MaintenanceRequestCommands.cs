using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.Requests
{
    public record CreateMaintenanceRequestCommand(SaveMaintenanceRequestDto Dto) : IRequest<MaintenanceRequestResponseDto>;
    public record UpdateMaintenanceRequestCommand(int Id, SaveMaintenanceRequestDto Dto) : IRequest<MaintenanceRequestResponseDto>;
    public record DeleteMaintenanceRequestCommand(int Id) : IRequest<Unit>;

    /// <summary>تغيير الحالة فقط (لوحة الحالات) — نفس نطاق التعديل</summary>
    public record ChangeMaintenanceRequestStatusCommand(int Id, int StatusId) : IRequest<MaintenanceRequestResponseDto>;

    /// <summary>نقل الطلب إلى فني آخر — رئيس القسم (داخل قسمه) أو السوبر ادمن</summary>
    public record AssignMaintenanceRequestCommand(int Id, int UserId) : IRequest<MaintenanceRequestResponseDto>;

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
                .Must(MaintenanceRules.IsValidPhone)
                .WithMessage("رقم الهاتف غير صحيح — مثال: 0933123456 أو 0112345678 أو ‎+963933123456");

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

    public class ChangeMaintenanceRequestStatusCommandValidator : AbstractValidator<ChangeMaintenanceRequestStatusCommand>
    {
        public ChangeMaintenanceRequestStatusCommandValidator() =>
            RuleFor(x => x.StatusId).GreaterThan(0).WithMessage("يجب اختيار حالة الطلب");
    }

    public class AssignMaintenanceRequestCommandValidator : AbstractValidator<AssignMaintenanceRequestCommand>
    {
        public AssignMaintenanceRequestCommandValidator() =>
            RuleFor(x => x.UserId).GreaterThan(0).WithMessage("يجب اختيار الموظف");
    }

    // ════════════════════ المعالج ════════════════════

    public class MaintenanceRequestCommandsHandler :
        IRequestHandler<CreateMaintenanceRequestCommand, MaintenanceRequestResponseDto>,
        IRequestHandler<UpdateMaintenanceRequestCommand, MaintenanceRequestResponseDto>,
        IRequestHandler<DeleteMaintenanceRequestCommand, Unit>,
        IRequestHandler<ChangeMaintenanceRequestStatusCommand, MaintenanceRequestResponseDto>,
        IRequestHandler<AssignMaintenanceRequestCommand, MaintenanceRequestResponseDto>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IDeviceTypeService _deviceTypeService;
        private readonly IDamageTypeService _damageTypeService;
        private readonly IDeviceCompanyService _companyService;
        private readonly IMaintenanceRequestStatusService _statusService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IMapper _mapper;

        public MaintenanceRequestCommandsHandler(
            IMaintenanceRequestService requestService,
            IDeviceTypeService deviceTypeService,
            IDamageTypeService damageTypeService,
            IDeviceCompanyService companyService,
            IMaintenanceRequestStatusService statusService,
            IUserService userService,
            INotificationService notifications,
            IMapper mapper)
        {
            _requestService = requestService;
            _deviceTypeService = deviceTypeService;
            _damageTypeService = damageTypeService;
            _companyService = companyService;
            _statusService = statusService;
            _userService = userService;
            _notifications = notifications;
            _mapper = mapper;
        }

        private async Task<MaintenanceRequest> LoadAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

        private async Task<MaintenanceRequestResponseDto> ToDtoAsync(int id, User viewer) =>
            MaintenanceRequestMapping.ToDto(_mapper, await LoadAsync(id), viewer);

        private Task LogAsync(MaintenanceRequest request, User actor, MaintenanceActivityType type, string text) =>
            _requestService.AddActivityAsync(new MaintenanceRequestActivity
            {
                MaintenanceRequestId = request.Id,
                UserId = actor.Id,
                Type = type,
                Text = text,
                CreatedAt = DateTime.UtcNow
            });

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
            r.ClientPhone = MaintenanceRules.NormalizePhone(r.ClientPhone); // يُحفظ أرقاماً متصلة
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

            await LogAsync(created, user, MaintenanceActivityType.Created, "سجّل الطلب");
            await MaintenanceNotifier.RequestCreatedAsync(_notifications, _userService, created, user);

            return await ToDtoAsync(created.Id, user);
        }

        public async Task<MaintenanceRequestResponseDto> Handle(UpdateMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.EnsureCanAccess(user, entity.UserId, entity.DepartmentId,
                "لا يمكنك تعديل طلب صيانة خارج نطاقك");

            await EnsureReferencesAsync(request.Dto);

            var oldStatusId = entity.MaintenanceRequestStatusId;
            var oldStatusName = entity.MaintenanceRequestStatus?.Name ?? "";

            // الفني والقسم وتاريخ الإنشاء لا تتغير (ليست في الـ DTO)
            _mapper.Map(request.Dto, entity);
            Trim(entity);
            entity.UpdatedAt = DateTime.UtcNow;

            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            var statusChanged = oldStatusId != updated.MaintenanceRequestStatusId;
            var what = statusChanged
                ? $"غيّر الحالة من «{oldStatusName}» إلى «{updated.MaintenanceRequestStatus?.Name}»"
                : "عدّل بيانات الطلب";

            await LogAsync(updated, user,
                statusChanged ? MaintenanceActivityType.StatusChanged : MaintenanceActivityType.Edited, what);

            await MaintenanceNotifier.RequestChangedAsync(_notifications, _userService, updated, user, what);

            return MaintenanceRequestMapping.ToDto(_mapper, updated, user);
        }

        public async Task<MaintenanceRequestResponseDto> Handle(ChangeMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.EnsureCanAccess(user, entity.UserId, entity.DepartmentId,
                "لا يمكنك تغيير حالة طلب صيانة خارج نطاقك");

            if (entity.MaintenanceRequestStatusId == request.StatusId)
                return MaintenanceRequestMapping.ToDto(_mapper, entity, user);

            if (!await _statusService.ExistsAsync(request.StatusId))
                throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");

            var oldStatusName = entity.MaintenanceRequestStatus?.Name ?? "";

            entity.MaintenanceRequestStatusId = request.StatusId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            var what = $"غيّر الحالة من «{oldStatusName}» إلى «{updated.MaintenanceRequestStatus?.Name}»";

            await LogAsync(updated, user, MaintenanceActivityType.StatusChanged, what);
            await MaintenanceNotifier.RequestChangedAsync(_notifications, _userService, updated, user, what);

            return MaintenanceRequestMapping.ToDto(_mapper, updated, user);
        }

        public async Task<MaintenanceRequestResponseDto> Handle(AssignMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            var target = await MaintenanceRules.ResolveAssigneeAsync(_userService, user, entity.DepartmentId, request.UserId);

            if (target.Id == entity.UserId)
                return MaintenanceRequestMapping.ToDto(_mapper, entity, user);

            var previousUserId = entity.UserId;
            var previousName = entity.User?.FullName ?? "";

            entity.UserId = target.Id;
            entity.DepartmentId = target.DepartmentId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);

            await LogAsync(updated, user, MaintenanceActivityType.Reassigned,
                $"نقل الطلب من {previousName} إلى {target.FullName}");
            await MaintenanceNotifier.RequestReassignedAsync(_notifications, updated, user, previousUserId, target);

            return MaintenanceRequestMapping.ToDto(_mapper, updated, user);
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

    /// <summary>التحويل إلى DTO مع ما يستطيعه المستخدم الحالي على السجل</summary>
    public static class MaintenanceRequestMapping
    {
        public static MaintenanceRequestResponseDto ToDto(IMapper mapper, MaintenanceRequest entity, User viewer)
        {
            var dto = mapper.Map<MaintenanceRequestResponseDto>(entity);
            dto.CanEdit = MaintenanceRules.CanAccess(viewer, entity.UserId, entity.DepartmentId);
            dto.CanAssign = MaintenanceRules.CanAssign(viewer, entity.DepartmentId);
            return dto;
        }
    }
}
