using Application.Common;
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

            RuleFor(x => x.DeviceMaintenanceId).GreaterThan(0).WithMessage("يجب اختيار الجهاز");
            RuleFor(x => x.DamageTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع العطل");
            RuleFor(x => x.MaintenanceRequestStatusId).GreaterThan(0).WithMessage("يجب اختيار حالة الطلب");

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
        private readonly IDeviceMaintenanceService _deviceService;
        private readonly IDamageTypeService _damageTypeService;
        private readonly IMaintenanceRequestStatusService _statusService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public MaintenanceRequestCommandsHandler(
            IMaintenanceRequestService requestService,
            IDeviceMaintenanceService deviceService,
            IDamageTypeService damageTypeService,
            IMaintenanceRequestStatusService statusService,
            IUserService userService,
            INotificationService notifications,
            IUserPermissionService permissions,
            IMapper mapper)
        {
            _permissions = permissions;
            _requestService = requestService;
            _deviceService = deviceService;
            _damageTypeService = damageTypeService;
            _statusService = statusService;
            _userService = userService;
            _notifications = notifications;
            _mapper = mapper;
        }

        private async Task<MaintenanceRequest> LoadAsync(int id) =>
            await _requestService.GetByIdAsync(id) ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

        private async Task<MaintenanceRequestResponseDto> ToDtoAsync(MaintenanceRequest entity) =>
            MaintenanceRequestMapping.ToDto(_mapper, entity, await MaintenanceRules.RequestScopesAsync(_userService, _permissions));

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
            if (!await _deviceService.ExistsAsync(dto.DeviceMaintenanceId))
                throw new KeyNotFoundException("الجهاز المحدد غير موجود — أضفه من أجهزة الصيانة أولاً");
            if (!await _damageTypeService.ExistsAsync(dto.DamageTypeId))
                throw new KeyNotFoundException("نوع العطل المحدد غير موجود");
            if (!await _statusService.ExistsAsync(dto.MaintenanceRequestStatusId))
                throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");
        }

        // حقول البحث تُحفظ بلا فراغات زائدة حتى يعمل "يبدأ بـ" بشكل صحيح
        private static void Trim(MaintenanceRequest r)
        {
            r.ClientName = r.ClientName.Trim();
            r.ClientPhone = MaintenanceRules.NormalizePhone(r.ClientPhone); // يُحفظ أرقاماً متصلة
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
            await MaintenanceNotifier.RequestCreatedAsync(_notifications, _permissions, created, user);

            return await ToDtoAsync(await LoadAsync(created.Id));
        }

        public async Task<MaintenanceRequestResponseDto> Handle(UpdateMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "EditMaintenanceRequest"), entity),
                "لا يمكنك تعديل طلب صيانة خارج نطاقك");

            await EnsureReferencesAsync(request.Dto);

            // تغيير الحالة عبر نموذج التعديل يحتاج صلاحية تغيير الحالة أيضاً (دون تعديل بقية البيانات إن لم يملكها)
            if (request.Dto.MaintenanceRequestStatusId != entity.MaintenanceRequestStatusId)
                MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.ChangeMaintenanceStatus), entity),
                    "لا تملك صلاحية تغيير حالة الطلب — أبقِ الحالة كما هي");

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

            await MaintenanceNotifier.RequestChangedAsync(_notifications, _permissions, updated, user, what);

            return await ToDtoAsync(updated);
        }

        public async Task<MaintenanceRequestResponseDto> Handle(ChangeMaintenanceRequestStatusCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.ChangeMaintenanceStatus), entity),
                "لا تملك صلاحية تغيير حالة هذا الطلب");

            if (entity.MaintenanceRequestStatusId == request.StatusId)
                return await ToDtoAsync(entity);

            if (!await _statusService.ExistsAsync(request.StatusId))
                throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");

            var oldStatusName = entity.MaintenanceRequestStatus?.Name ?? "";

            entity.MaintenanceRequestStatusId = request.StatusId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            var what = $"غيّر الحالة من «{oldStatusName}» إلى «{updated.MaintenanceRequestStatus?.Name}»";

            await LogAsync(updated, user, MaintenanceActivityType.StatusChanged, what);
            await MaintenanceNotifier.RequestChangedAsync(_notifications, _permissions, updated, user, what);

            return await ToDtoAsync(updated);
        }

        public async Task<MaintenanceRequestResponseDto> Handle(AssignMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            var assign = await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceRequest");
            MaintenanceRules.Ensure(MaintenanceRules.In(assign, entity), "لا يمكنك نقل طلب صيانة خارج نطاقك");

            var target = await MaintenanceRules.ResolveAssigneeAsync(_userService, assign, request.UserId);

            if (target.Id == entity.UserId)
                return await ToDtoAsync(entity);

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

            return await ToDtoAsync(updated);
        }

        public async Task<Unit> Handle(DeleteMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "DeleteMaintenanceRequest"), entity),
                "لا يمكنك حذف طلب صيانة خارج نطاقك");

            await _requestService.DeleteAsync(entity);
            return Unit.Value;
        }
    }

    /// <summary>التحويل إلى DTO مع ما يستطيعه المستخدم الحالي على السجل</summary>
    public static class MaintenanceRequestMapping
    {
        public static MaintenanceRequestResponseDto ToDto(IMapper mapper, MaintenanceRequest entity, MaintenanceRules.Scopes scopes)
        {
            var dto = mapper.Map<MaintenanceRequestResponseDto>(entity);
            dto.CanEdit = MaintenanceRules.In(scopes.Edit, entity);
            dto.CanDelete = MaintenanceRules.In(scopes.Delete, entity);
            dto.CanAssign = MaintenanceRules.In(scopes.Assign, entity);
            dto.CanChangeStatus = MaintenanceRules.In(scopes.Status, entity);
            return dto;
        }
    }
}
