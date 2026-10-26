using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Users;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Attendance;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Common.AppPermissions;

namespace Application.Features.Attendance
{
    public record SearchAttendancePeopleQuery(AttendancePeopleSearchDto Dto) : IRequest<PagedResultDto<AttendancePersonDto>>;
    public record GetAttendancePersonQuery(int UserId) : IRequest<AttendancePersonDto>;
    public record UpdateAttendanceProfileCommand(int UserId, AttendanceProfileRequestDto Dto) : IRequest<AttendanceProfileSavedDto>;
    public record CreateDailyWorkerCommand(DailyWorkerRequestDto Dto) : IRequest<AttendanceProfileSavedDto>;
    public record UpdateDailyWorkerCommand(int UserId, DailyWorkerRequestDto Dto) : IRequest<AttendanceProfileSavedDto>;
    public record DeleteDailyWorkerCommand(int UserId) : IRequest<Unit>;
    public record ClearPersonBiometricsCommand(int UserId) : IRequest<AttendancePersonDto>;

    public class DailyWorkerRequestValidator : AbstractValidator<DailyWorkerRequestDto>
    {
        public DailyWorkerRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().WithMessage("اسم العامل مطلوب").MaximumLength(150).WithMessage("الاسم طويل جداً");
            RuleFor(x => x.AreaId).NotNull().GreaterThan(0).WithMessage("منطقة عمل العامل مطلوبة");
            RuleFor(x => x.PhoneNumber).Must(PhoneRules.IsValid)
                .WithMessage("رقم التواصل غير صحيح: جوال 09XXXXXXXX أو أرضي يبدأ بـ 0 ورمز المحافظة");
            RuleFor(x => x.PersonalIdNumber)
                .Must(v => System.Text.RegularExpressions.Regex.IsMatch(v!.Trim(), "^[A-Za-z0-9-]{1,20}$"))
                .WithMessage("الرقم الذاتي: حروف لاتينية وأرقام فقط، حتى 20 خانة")
                .When(x => !string.IsNullOrWhiteSpace(x.PersonalIdNumber));
        }
    }

    public class CreateDailyWorkerCommandValidator : AbstractValidator<CreateDailyWorkerCommand>
    {
        public CreateDailyWorkerCommandValidator() => RuleFor(x => x.Dto).SetValidator(new DailyWorkerRequestValidator());
    }

    public class UpdateDailyWorkerCommandValidator : AbstractValidator<UpdateDailyWorkerCommand>
    {
        public UpdateDailyWorkerCommandValidator() => RuleFor(x => x.Dto).SetValidator(new DailyWorkerRequestValidator());
    }

    /// <summary>
    /// الأشخاص في الحضور: القائمة بحدود صلاحيات العرض (ولمن يدير الأشخاص: أشخاص منطقته)، بيانات الحضور
    /// (EditAttendanceProfile)، وعمال المياومة (Create/Edit/DeleteDailyWorker) — الحد: منطقتي، أو الكل مع ViewOrganizationAttendance.
    /// كل تغيير يعلّم أجهزة المناطق المعنية فتعيد الخدمة الخلفية مزامنتها.
    /// </summary>
    public class AttendancePeopleHandler :
        IRequestHandler<SearchAttendancePeopleQuery, PagedResultDto<AttendancePersonDto>>,
        IRequestHandler<GetAttendancePersonQuery, AttendancePersonDto>,
        IRequestHandler<UpdateAttendanceProfileCommand, AttendanceProfileSavedDto>,
        IRequestHandler<CreateDailyWorkerCommand, AttendanceProfileSavedDto>,
        IRequestHandler<UpdateDailyWorkerCommand, AttendanceProfileSavedDto>,
        IRequestHandler<DeleteDailyWorkerCommand, Unit>,
        IRequestHandler<ClearPersonBiometricsCommand, AttendancePersonDto>
    {
        private readonly IAttendancePeopleService _people;
        private readonly IAttendanceSyncService _sync;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IBranchService _branches;
        private readonly IDepartmentService _departments;
        private readonly IOfficeService _offices;
        private readonly IRoleService _roles;

        public AttendancePeopleHandler(IAttendancePeopleService people, IAttendanceSyncService sync, IUserService users,
            IUserPermissionService permissions, IBranchService branches, IDepartmentService departments, IOfficeService offices, IRoleService roles)
        {
            _people = people;
            _sync = sync;
            _users = users;
            _permissions = permissions;
            _branches = branches;
            _departments = departments;
            _offices = offices;
            _roles = roles;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_users, _permissions);

        private static bool ManagesPeople(Viewer v) =>
            v.IsSuperAdmin || v.Has(EditAttendanceProfile) || v.Has(CreateDailyWorker) || v.Has(EditDailyWorker) || v.Has(DeleteDailyWorker);

        /// <summary>من يراهم: حدّ صلاحيات العرض، ولمن يدير الأشخاص منطقته أيضاً (أو الكل)</summary>
        public static AttendanceScope PeopleScope(Viewer viewer)
        {
            var view = AttendanceRules.ViewScope(viewer);
            if (!ManagesPeople(viewer)) return view;
            var manage = AttendanceRules.ManageLimit(viewer);
            if (manage.All) return AttendanceScope.Everything;
            return view.AreaId == null && manage.AreaId != null ? view with { AreaId = manage.AreaId } : view;
        }

        private static bool Includes(AttendanceScope scope, PersonRow p) =>
            scope.All
            || (scope.SelfId != null && p.UserId == scope.SelfId)
            || (scope.DepartmentId != null && p.DepartmentId == scope.DepartmentId)
            || (scope.BranchId != null && p.BranchId == scope.BranchId)
            || (scope.AreaId != null && (p.HomeAreaId == scope.AreaId || p.ExtraAreaIds.Contains(scope.AreaId.Value)));

        private async Task<(PersonRow Row, Viewer Viewer)> VisibleAsync(int userId)
        {
            var viewer = await ViewerAsync();
            var row = await _people.GetRowAsync(userId);
            if (row == null || !Includes(PeopleScope(viewer), row)) throw new KeyNotFoundException("الشخص غير موجود");
            return (row, viewer);
        }

        private AttendancePersonDto ToDto(PersonRow r, Viewer viewer, Dictionary<int, string> areaNames, Dictionary<int, int> devicesPerArea)
        {
            var manage = AttendanceRules.ManageLimit(viewer);
            var manages = manage.Includes(r.HomeAreaId);
            var worker = r.Kind == UserKind.DailyWorker;
            var areas = r.ExtraAreaIds.Append(r.HomeAreaId ?? 0).Where(a => a > 0).Distinct();
            return new AttendancePersonDto
            {
                UserId = r.UserId, FullName = r.FullName, Kind = (int)r.Kind, KindName = AttendanceRules.KindName(r.Kind), IsActive = r.IsActive,
                PersonalIdNumber = r.PersonalIdNumber, PhoneNumber = r.PhoneNumber,
                BranchId = r.BranchId, BranchName = r.BranchName, DepartmentId = r.DepartmentId, DepartmentName = r.DepartmentName,
                OfficeId = r.OfficeId, OfficeName = r.OfficeName,
                AttendanceNumber = r.AttendanceNumber, CardNumber = r.CardNumber, IsDeviceAdmin = r.IsDeviceAdmin,
                AreaId = r.HomeAreaId, AreaName = r.HomeAreaId is int a && areaNames.TryGetValue(a, out var n) ? n : string.Empty,
                WorkerAreaId = r.WorkerAreaId,
                ExtraAreas = r.ExtraAreaIds.Select(id => new IdNameDto { Id = id, Name = areaNames.GetValueOrDefault(id, string.Empty) }).ToList(),
                ActiveFrom = r.ActiveFrom, ActiveTo = r.ActiveTo,
                Faces = r.Faces, Fingers = r.Fingers, HasBioPhoto = r.HasBioPhoto,
                OnDevices = r.OnDevices,
                ExpectedDevices = r.IsActive && r.AttendanceNumber != null ? areas.Sum(id => devicesPerArea.GetValueOrDefault(id)) : 0,
                CanEditProfile = manages && (viewer.IsSuperAdmin || viewer.Has(EditAttendanceProfile)),
                CanEditWorker = worker && manages && (viewer.IsSuperAdmin || viewer.Has(EditDailyWorker)),
                CanDeleteWorker = worker && manages && (viewer.IsSuperAdmin || viewer.Has(DeleteDailyWorker))
            };
        }

        private async Task<AttendancePersonDto> DtoAsync(int userId, Viewer viewer) =>
            ToDto(await _people.GetRowAsync(userId) ?? throw new KeyNotFoundException("الشخص غير موجود"),
                viewer, await _people.AreaNamesAsync(), await _people.DevicesPerAreaAsync());

        public async Task<PagedResultDto<AttendancePersonDto>> Handle(SearchAttendancePeopleQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var d = request.Dto;
            var page = Math.Max(1, d.Page);
            var size = Math.Clamp(d.PageSize, 1, 200);
            var kind = d.Kind is int k && Enum.IsDefined(typeof(UserKind), k) ? (UserKind?)k : null;
            var (items, total) = await _people.SearchAsync(PeopleScope(viewer),
                new PeopleFilter(kind, d.AreaId, d.BranchId, d.DepartmentId, d.Query, d.Missing, d.Active), page, size);
            var areaNames = await _people.AreaNamesAsync();
            var devices = await _people.DevicesPerAreaAsync();
            return new PagedResultDto<AttendancePersonDto>
            {
                Page = page, PageSize = size, TotalCount = total,
                Items = items.Select(r => ToDto(r, viewer, areaNames, devices)).ToList()
            };
        }

        public async Task<AttendancePersonDto> Handle(GetAttendancePersonQuery request, CancellationToken ct)
        {
            var (row, viewer) = await VisibleAsync(request.UserId);
            return ToDto(row, viewer, await _people.AreaNamesAsync(), await _people.DevicesPerAreaAsync());
        }

        /// <summary>يتحقق من بيانات الحضور ويطبّقها على السجل، ويعيد المناطق الإضافية النظيفة</summary>
        private async Task<List<int>> ApplyProfileAsync(AttendanceProfile profile, AttendanceProfileRequestDto dto, int userId, int? homeArea)
        {
            var number = AttendanceRules.NormalizeNumber(dto.AttendanceNumber);
            if (number != null && await _people.NumberOwnerAsync(number) is int owner && owner != userId)
                throw new InvalidOperationException($"رقم البصمة {number} مسجّل لشخص آخر");
            var card = AttendanceRules.NormalizeCard(dto.CardNumber);
            if (card != null && await _people.CardOwnerAsync(card) is int cardOwner && cardOwner != userId)
                throw new InvalidOperationException($"رقم البطاقة {card} مسجّل لشخص آخر");
            if (dto.ActiveFrom != null && dto.ActiveTo != null && dto.ActiveFrom.Value.Date > dto.ActiveTo.Value.Date)
                throw new InvalidOperationException("بداية فترة العمل بعد نهايتها");

            var known = await _people.AreaNamesAsync();
            var extras = dto.ExtraAreaIds.Distinct().Where(id => id != homeArea).ToList();
            if (extras.Count > AttendanceRules.MaxExtraAreas)
                throw new InvalidOperationException($"المناطق الإضافية {AttendanceRules.MaxExtraAreas} على الأكثر");
            if (extras.Any(id => !known.ContainsKey(id))) throw new KeyNotFoundException("إحدى المناطق الإضافية غير موجودة");

            profile.AttendanceNumber = number;
            profile.CardNumber = card;
            profile.IsDeviceAdmin = dto.IsDeviceAdmin;
            profile.ActiveFrom = dto.ActiveFrom?.Date;
            profile.ActiveTo = dto.ActiveTo?.Date;
            profile.UpdatedAt = DateTime.UtcNow;
            return extras;
        }

        private static List<int> AreasOf(AttendanceProfile? profile) =>
            profile == null ? [] : profile.ExtraAreas.Select(a => a.AreaId).Append(profile.AreaId ?? 0).Where(a => a > 0).ToList();

        public async Task<AttendanceProfileSavedDto> Handle(UpdateAttendanceProfileCommand request, CancellationToken ct)
        {
            var (row, viewer) = await VisibleAsync(request.UserId);
            AttendanceRules.EnsureManage(AttendanceRules.ManageLimit(viewer), row.HomeAreaId);

            var profile = await _people.GetProfileAsync(request.UserId);
            var isNew = profile == null;
            profile ??= new AttendanceProfile { UserId = request.UserId };
            var previousAreas = AreasOf(isNew ? null : profile);
            var previousNumber = profile.AttendanceNumber;
            var extras = await ApplyProfileAsync(profile, request.Dto, request.UserId, row.HomeAreaId);
            await _people.SaveProfileAsync(profile, isNew, extras);

            var linked = request.Dto.LinkUnknownPunches && profile.AttendanceNumber != null && profile.AttendanceNumber != previousNumber
                ? await _people.LinkUnknownPunchesAsync(profile.AttendanceNumber, request.UserId) : 0;
            await _sync.MarkUsersAsync([request.UserId], previousAreaIds: previousAreas);
            return new AttendanceProfileSavedDto { Person = await DtoAsync(request.UserId, viewer), LinkedPunches = linked };
        }

        /// <summary>منطقة العامل وفرعه (الفرع إن وُجد يتبع المنطقة)، ومكانه المشتق من الأدق</summary>
        private async Task<(int AreaId, int? BranchId, int? DepartmentId, int? OfficeId)> WorkerPlacementAsync(DailyWorkerRequestDto dto, AreaLimit manage)
        {
            var areaId = dto.AreaId!.Value;
            if (!(await _people.AreaNamesAsync()).ContainsKey(areaId)) throw new KeyNotFoundException("المنطقة غير موجودة");
            AttendanceRules.EnsureManage(manage, areaId, "يمكنك إدارة عمال منطقتك فقط");
            var placement = await UserRules.EnsureUserReferencesAsync(_branches, _departments, _offices, _roles,
                dto.BranchId, dto.DepartmentId, dto.OfficeId, null);
            if (placement.BranchId is int branchId && (await _branches.GetByIdAsync(branchId))?.AttendanceAreaId != areaId)
                throw new InvalidOperationException("الفرع المختار لا يتبع منطقة عمل العامل");
            return (areaId, placement.BranchId, placement.DepartmentId, placement.OfficeId);
        }

        public async Task<AttendanceProfileSavedDto> Handle(CreateDailyWorkerCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var dto = request.Dto;
            var place = await WorkerPlacementAsync(dto, AttendanceRules.ManageLimit(viewer));
            var personalId = UserRules.NormalizePersonalIdNumber(dto.PersonalIdNumber);
            if (personalId != null && !await _users.IsPersonalIdNumberUniqueAsync(personalId))
                throw new InvalidOperationException("الرقم الذاتي مسجّل لشخص آخر");

            var user = new User
            {
                Kind = UserKind.DailyWorker, FullName = dto.FullName.Trim(), PersonalIdNumber = personalId,
                PhoneNumber = PhoneRules.ToStored(dto.PhoneNumber), BranchId = place.BranchId, DepartmentId = place.DepartmentId,
                OfficeId = place.OfficeId, IsActive = true, CreatedAt = DateTime.UtcNow
            };
            var profile = new AttendanceProfile { AreaId = place.AreaId };
            var extras = await ApplyProfileAsync(profile, dto, 0, place.AreaId);
            await _people.AddPersonAsync(user, profile, extras);

            var linked = dto.LinkUnknownPunches && profile.AttendanceNumber != null
                ? await _people.LinkUnknownPunchesAsync(profile.AttendanceNumber, user.Id) : 0;
            await _sync.MarkUsersAsync([user.Id]);
            return new AttendanceProfileSavedDto { Person = await DtoAsync(user.Id, viewer), LinkedPunches = linked };
        }

        private async Task<(User User, AttendanceProfile Profile, bool IsNew, Viewer Viewer)> WorkerAsync(int userId)
        {
            var (row, viewer) = await VisibleAsync(userId);
            if (row.Kind != UserKind.DailyWorker) throw new InvalidOperationException("هذا ليس عامل مياومة — يُعدَّل من صفحة المستخدمين");
            AttendanceRules.EnsureManage(AttendanceRules.ManageLimit(viewer), row.HomeAreaId, "يمكنك إدارة عمال منطقتك فقط");
            var user = await _people.GetUserAsync(userId) ?? throw new KeyNotFoundException("الشخص غير موجود");
            var existing = await _people.GetProfileAsync(userId);
            return (user, existing ?? new AttendanceProfile { UserId = userId }, existing == null, viewer);
        }

        public async Task<AttendanceProfileSavedDto> Handle(UpdateDailyWorkerCommand request, CancellationToken ct)
        {
            var (user, profile, isNew, viewer) = await WorkerAsync(request.UserId);
            var dto = request.Dto;
            var place = await WorkerPlacementAsync(dto, AttendanceRules.ManageLimit(viewer));
            var personalId = UserRules.NormalizePersonalIdNumber(dto.PersonalIdNumber);
            if (personalId != null && !await _users.IsPersonalIdNumberUniqueAsync(personalId, user.Id))
                throw new InvalidOperationException("الرقم الذاتي مسجّل لشخص آخر");

            var previousAreas = AreasOf(profile);
            var previousBranch = user.BranchId;
            var previousNumber = profile.AttendanceNumber;

            user.FullName = dto.FullName.Trim();
            user.PersonalIdNumber = personalId;
            user.PhoneNumber = PhoneRules.ToStored(dto.PhoneNumber);
            user.BranchId = place.BranchId;
            user.DepartmentId = place.DepartmentId;
            user.OfficeId = place.OfficeId;
            user.IsActive = dto.IsActive;
            profile.AreaId = place.AreaId;
            var extras = await ApplyProfileAsync(profile, dto, user.Id, place.AreaId);
            await _people.SaveProfileAsync(profile, isNew, extras);

            var linked = dto.LinkUnknownPunches && profile.AttendanceNumber != null && profile.AttendanceNumber != previousNumber
                ? await _people.LinkUnknownPunchesAsync(profile.AttendanceNumber, user.Id) : 0;
            await _sync.MarkUsersAsync([user.Id], previousBranch is int b ? [b] : null, previousAreas);
            return new AttendanceProfileSavedDto { Person = await DtoAsync(user.Id, viewer), LinkedPunches = linked };
        }

        public async Task<Unit> Handle(DeleteDailyWorkerCommand request, CancellationToken ct)
        {
            var (user, _, _, _) = await WorkerAsync(request.UserId);
            if (await _people.DeleteBlockerAsync(user.Id) is string blocker) throw new InvalidOperationException(blocker);
            // تُعلَّم أجهزته قبل الحذف (بعده لا تُعرف مناطقه) فتزيله المزامنة منها
            await _sync.MarkUsersAsync([user.Id]);
            await _people.DeleteUserAsync(user);
            return Unit.Value;
        }

        public async Task<AttendancePersonDto> Handle(ClearPersonBiometricsCommand request, CancellationToken ct)
        {
            var (row, viewer) = await VisibleAsync(request.UserId);
            AttendanceRules.EnsureManage(AttendanceRules.ManageLimit(viewer), row.HomeAreaId);
            await _people.ClearBiometricsAsync(request.UserId);
            return await DtoAsync(request.UserId, viewer);
        }
    }
}
