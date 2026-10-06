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
            // العميل الموظف يُؤخذ اسمه من حسابه؛ الاسم مطلوب للعميل الخارجي فقط
            RuleFor(x => x.ClientName)
                .NotEmpty().WithMessage("اسم العميل مطلوب").When(x => x.ClientUserId == null)
                .MaximumLength(200).WithMessage("اسم العميل لا يتجاوز 200 حرف");

            RuleFor(x => x.ClientPhone)
                .MaximumLength(30).WithMessage("رقم الهاتف لا يتجاوز 30 خانة")
                .Must(MaintenanceRules.IsValidPhone)
                .WithMessage("رقم الهاتف غير صحيح — مثال: 0933123456 أو 0112345678 أو ‎+963933123456");

            RuleFor(x => x.DeviceMaintenanceId).GreaterThan(0).WithMessage("يجب اختيار الجهاز");
            RuleFor(x => x.AssigneeId).GreaterThan(0).When(x => x.AssigneeId != null).WithMessage("الفني المسؤول غير صحيح");
            RuleFor(x => x.DamageTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع العطل");
            RuleFor(x => x.MaintenanceRequestStatusId).GreaterThan(0).WithMessage("يجب اختيار حالة الطلب");

            RuleFor(x => x.Accessories).MaximumLength(500).WithMessage("الملحقات لا تتجاوز 500 حرف");
            RuleFor(x => x.Description).MaximumLength(2000).WithMessage("الوصف لا يتجاوز 2000 حرف");

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
        private readonly IUserSignatureService _signatures;
        private readonly ISparePartService _spareParts;
        private readonly IMapper _mapper;

        public MaintenanceRequestCommandsHandler(
            IMaintenanceRequestService requestService,
            IDeviceMaintenanceService deviceService,
            IDamageTypeService damageTypeService,
            IMaintenanceRequestStatusService statusService,
            IUserService userService,
            INotificationService notifications,
            IUserPermissionService permissions,
            IUserSignatureService signatures,
            ISparePartService spareParts,
            IMapper mapper)
        {
            _permissions = permissions;
            _signatures = signatures;
            _spareParts = spareParts;
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

        private Task NotifyClientStatusAsync(MaintenanceRequest r, User actor)
        {
            var number = MaintenanceRules.RequestNumber(r.Id, r.CreatedAt);
            var (title, message) = r.MaintenanceRequestStatus?.Stage switch
            {
                MaintenanceStage.Ready => ("جهازك جاهز للاستلام", $"انتهت صيانة جهازك — الطلب {number}. يمكنك استلامه من قسم الصيانة"),
                MaintenanceStage.NotRepairable => ("تعذّر إصلاح جهازك", $"الجهاز غير قابل للصيانة — الطلب {number}. راجع قسم الصيانة لاستلامه"),
                MaintenanceStage.Delivered => ("سُلّم جهازك", $"سُلّم جهازك — الطلب {number}"),
                _ => ("تحديث على جهازك في الصيانة", $"صارت حالة طلب الصيانة {number} لجهازك: {r.MaintenanceRequestStatus?.Name}")
            };
            return MaintenanceNotifier.ClientUpdateAsync(_notifications, r, actor, title, message);
        }

        /// <summary>دخل الطلب مرحلة نهائية: أي طلب تحويل معلّق لم يعد له معنى</summary>
        private async Task ClosePendingTransferIfFinalAsync(MaintenanceRequest r, User actor)
        {
            if (!MaintenanceRules.IsClosed(r)) return;
            if (await _requestService.GetPendingTransferAsync(r.Id) is { } pending
                && await _requestService.GetTransferAsync(pending.Id) is { } transfer)
            {
                transfer.Status = MaintenanceTransferStatus.Closed;
                transfer.DecidedById = actor.Id;
                transfer.DecidedAt = DateTime.UtcNow;
                transfer.DecisionNote = "أُغلق الطلب قبل البت في طلب التحويل";
                await _requestService.UpdateTransferAsync(transfer);
            }
        }

        /// <summary>
        /// العميل موظف (ClientUserId): الاسم من حسابه، والهاتف المكتوب أو هاتفه المسجّل؛ وإلا عميل خارجي بالاسم والهاتف المكتوبين
        /// </summary>
        private async Task ApplyClientAsync(MaintenanceRequest entity, SaveMaintenanceRequestDto dto)
        {
            if (dto.ClientUserId is int clientId)
            {
                var client = await MaintenanceClients.ResolveAsync(_userService, clientId);
                entity.ClientUserId = client.Id;
                entity.ClientName = client.FullName;
                if (string.IsNullOrWhiteSpace(dto.ClientPhone)) entity.ClientPhone = client.PhoneNumber ?? string.Empty;
            }
            else entity.ClientUserId = null;
        }

        /// <summary>
        /// عند دخول الطلب مرحلة «مُسلَّم»: يُثبَّت موقّع ورقة التسليم — صاحب SignMaintenanceReceipt في قسم الطلب
        /// (الأقدم إن تعدّدوا) — ونسخة توقيعه الحالية، فلا تتغير الورقة إن تغيّر التوقيع أو الموقّع لاحقاً.
        /// كل دخول جديد لحالة تسليم (بعد إعادة الجهاز للصيانة مثلاً) يُثبِّت من جديد.
        /// </summary>
        private async Task StampDeliveryAsync(MaintenanceRequest entity, int? previousStatusId)
        {
            if (entity.MaintenanceRequestStatusId == previousStatusId) return;
            var status = await _statusService.GetByIdAsync(entity.MaintenanceRequestStatusId);
            if (status is not { Stage: MaintenanceStage.Delivered }) return;

            var signer = entity.DepartmentId is int departmentId
                ? (await _permissions.GetUsersWithPermissionAsync(AppPermissions.SignMaintenanceReceipt, departmentId: departmentId))
                    .OrderBy(u => u.Id).FirstOrDefault()
                : null;

            entity.DeliveredAt = DateTime.UtcNow;
            entity.DeliverySignerId = signer?.Id;
            entity.DeliverySignatureId = signer == null ? null : await _signatures.GetCurrentIdAsync(signer.Id);
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
            await ApplyClientAsync(entity, request.Dto);
            Trim(entity);

            // الفني = من سجّل الطلب، أو موظف من قسمه يسنده إليه من يملك AssignMaintenanceRequest (قرار المستخدم 2026-10-05).
            // القسم يُحفظ لحظة التسجيل (قسم الفني) ليراه رئيس القسم؛ المسجِّل يبقى في سجل الطلب فقط
            User? assignee = null;
            if (request.Dto.AssigneeId is int assigneeId && assigneeId != user.Id)
                assignee = await MaintenanceRules.ResolveAssigneeAsync(_userService,
                    await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceRequest"), assigneeId);

            entity.UserId = assignee?.Id ?? user.Id;
            entity.DepartmentId = assignee?.DepartmentId ?? user.DepartmentId;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
            var initial = await _statusService.GetByIdAsync(entity.MaintenanceRequestStatusId)
                ?? throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");
            if (MaintenanceRules.IsFinal(initial.Stage))
                throw new InvalidOperationException("لا يُسجَّل طلب جديد في حالة نهائية (مُسلَّم أو غير قابل للصيانة)");
            MaintenanceRules.ApplyStageTimes(entity, initial.Stage);
            await StampDeliveryAsync(entity, null);

            var created = await _requestService.AddAsync(entity);

            await LogAsync(created, user, MaintenanceActivityType.Created,
                assignee == null ? "سجّل الطلب" : $"سجّل الطلب وأسنده إلى {assignee.FullName}");
            await MaintenanceNotifier.RequestCreatedAsync(_notifications, _permissions, created, user);
            await MaintenanceNotifier.ClientUpdateAsync(_notifications, created, user, "استُلم جهازك للصيانة",
                $"سُجّل طلب الصيانة {MaintenanceRules.RequestNumber(created.Id, created.CreatedAt)} لجهازك — الحالة: {(await LoadAsync(created.Id)).MaintenanceRequestStatus?.Name}");
            if (assignee != null)
                await MaintenanceNotifier.RequestReassignedAsync(_notifications, created, user, user.Id, assignee);

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

            MaintenanceRules.EnsureOpen(entity);

            var oldStatusId = entity.MaintenanceRequestStatusId;
            var oldStatusName = entity.MaintenanceRequestStatus?.Name ?? "";

            // الفني والقسم وتاريخ الإنشاء لا تتغير (ليست في الـ DTO)
            _mapper.Map(request.Dto, entity);
            await ApplyClientAsync(entity, request.Dto);
            Trim(entity);
            entity.UpdatedAt = DateTime.UtcNow;
            if (entity.MaintenanceRequestStatusId != oldStatusId)
                MaintenanceRules.ApplyStageTimes(entity, (await _statusService.GetByIdAsync(entity.MaintenanceRequestStatusId))!.Stage);
            await StampDeliveryAsync(entity, oldStatusId);

            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            var statusChanged = oldStatusId != updated.MaintenanceRequestStatusId;
            var what = statusChanged
                ? $"غيّر الحالة من «{oldStatusName}» إلى «{updated.MaintenanceRequestStatus?.Name}»"
                : "عدّل بيانات الطلب";

            await LogAsync(updated, user,
                statusChanged ? MaintenanceActivityType.StatusChanged : MaintenanceActivityType.Edited, what);

            await MaintenanceNotifier.RequestChangedAsync(_notifications, _permissions, updated, user, what);
            if (statusChanged) { await NotifyClientStatusAsync(updated, user); await ClosePendingTransferIfFinalAsync(updated, user); }

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

            MaintenanceRules.EnsureOpen(entity);
            var newStatus = await _statusService.GetByIdAsync(request.StatusId)
                ?? throw new KeyNotFoundException("حالة الطلب المحددة غير موجودة");

            var oldStatusName = entity.MaintenanceRequestStatus?.Name ?? "";

            var previousStatusId = entity.MaintenanceRequestStatusId;
            entity.MaintenanceRequestStatusId = request.StatusId;
            entity.UpdatedAt = DateTime.UtcNow;
            MaintenanceRules.ApplyStageTimes(entity, newStatus.Stage);
            await StampDeliveryAsync(entity, previousStatusId);
            await _requestService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            var what = $"غيّر الحالة من «{oldStatusName}» إلى «{updated.MaintenanceRequestStatus?.Name}»";

            await LogAsync(updated, user, MaintenanceActivityType.StatusChanged, what);
            await MaintenanceNotifier.RequestChangedAsync(_notifications, _permissions, updated, user, what);
            await NotifyClientStatusAsync(updated, user);
            await ClosePendingTransferIfFinalAsync(updated, user);

            return await ToDtoAsync(updated);
        }

        public async Task<MaintenanceRequestResponseDto> Handle(AssignMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            var assign = await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceRequest");
            MaintenanceRules.Ensure(MaintenanceRules.In(assign, entity), "لا يمكنك نقل طلب صيانة خارج نطاقك");
            MaintenanceRules.EnsureOpen(entity);

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

            // طلب تحويل معلّق صار بلا معنى بعد النقل المباشر — يُغلق تلقائياً
            if (await _requestService.GetPendingTransferAsync(updated.Id) is { } pending
                && await _requestService.GetTransferAsync(pending.Id) is { } transfer)
            {
                transfer.Status = MaintenanceTransferStatus.Closed;
                transfer.DecidedById = user.Id;
                transfer.DecidedAt = DateTime.UtcNow;
                transfer.NewUserId = target.Id;
                transfer.DecisionNote = "نُقل الطلب مباشرة قبل البت في طلب التحويل";
                await _requestService.UpdateTransferAsync(transfer);
            }

            return await ToDtoAsync(updated);
        }

        public async Task<Unit> Handle(DeleteMaintenanceRequestCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.Ensure(MaintenanceRules.In(await MaintenanceRules.BoundaryAsync(_userService, _permissions, "DeleteMaintenanceRequest"), entity),
                "لا يمكنك حذف طلب صيانة خارج نطاقك");

            // حركات المخزون (صرف وإرجاع) سجل لا يُحذف ويشير إلى الطلب — فلا يُحذف طلب صُرفت عليه قطع
            if (await _spareParts.HasRequestPartsAsync(entity.Id))
                throw new InvalidOperationException("لا يمكن حذف طلب صُرفت عليه قطع غيار — سجل المخزون يشير إليه");

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
            // طلب التحويل: الطلب مسند إليّ وأملك الصلاحية (وجود طلب معلّق يُفحص عند الإرسال)
            dto.CanRequestTransfer = scopes.Transfer != null && entity.UserId == scopes.Transfer.OwnerId;

            // الطلب المُغلق (مُسلَّم / غير قابل للصيانة): للقراءة فقط — الحذف يبقى لصاحب صلاحيته
            dto.IsClosed = MaintenanceRules.IsClosed(entity);
            if (dto.IsClosed) dto.CanEdit = dto.CanChangeStatus = dto.CanAssign = dto.CanRequestTransfer = false;
            return dto;
        }
    }
}
