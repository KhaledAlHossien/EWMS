using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Attendance;
using FluentValidation;
using MediatR;

namespace Application.Features.Attendance.Shifts
{
    public record GetAttendanceSchedulesQuery(int? TargetType, int? TargetId) : IRequest<List<AttendanceScheduleDto>>;
    public record CreateAttendanceScheduleCommand(AttendanceScheduleRequestDto Dto) : IRequest<AttendanceScheduleDto>;
    public record UpdateAttendanceScheduleCommand(int Id, AttendanceScheduleRequestDto Dto) : IRequest<AttendanceScheduleDto>;
    public record DeleteAttendanceScheduleCommand(int Id) : IRequest<Unit>;
    public record GetAttendanceTemporariesQuery(int? TargetType, int? TargetId) : IRequest<List<AttendanceTemporaryDto>>;
    public record CreateAttendanceTemporaryCommand(AttendanceTemporaryRequestDto Dto) : IRequest<AttendanceTemporaryDto>;
    public record DeleteAttendanceTemporaryCommand(int Id) : IRequest<Unit>;
    public record GetScheduleTargetsQuery(int Type, string? Query) : IRequest<List<ScheduleTargetDto>>;
    public record GetEffectiveScheduleQuery(int UserId, DateTime From, DateTime To) : IRequest<AttendanceEffectiveDto>;
    public record GetUnscheduledPeopleQuery(DateTime? Date) : IRequest<List<UnscheduledPersonDto>>;

    public class AttendanceScheduleRequestValidator : AbstractValidator<AttendanceScheduleRequestDto>
    {
        public AttendanceScheduleRequestValidator()
        {
            RuleFor(x => x.TargetType).InclusiveBetween(1, 5).WithMessage("نوع الجهة غير صالح");
            RuleFor(x => x.TargetId).GreaterThan(0).WithMessage("اختر الجهة");
            RuleFor(x => x.ShiftId).GreaterThan(0).WithMessage("اختر الوردية");
            RuleFor(x => x.FromDate).NotEmpty().WithMessage("تاريخ البداية مطلوب");
            RuleFor(x => x.ToDate).Must((dto, to) => to == null || to.Value.Date >= dto.FromDate.Date)
                .WithMessage("تاريخ النهاية قبل تاريخ البداية");
            RuleFor(x => x.Note).MaximumLength(300).WithMessage("الملاحظة طويلة جداً");
        }
    }

    public class AttendanceTemporaryRequestValidator : AbstractValidator<AttendanceTemporaryRequestDto>
    {
        public AttendanceTemporaryRequestValidator()
        {
            RuleFor(x => x.TargetType).InclusiveBetween(1, 5).WithMessage("نوع الجهة غير صالح");
            RuleFor(x => x.TargetId).GreaterThan(0).WithMessage("اختر الجهة");
            RuleFor(x => x.FromDate).NotEmpty().WithMessage("تاريخ البداية مطلوب");
            RuleFor(x => x.ToDate).Must((dto, to) => to.Date >= dto.FromDate.Date).WithMessage("تاريخ النهاية قبل تاريخ البداية");
            RuleFor(x => x.ToDate).Must((dto, to) => (to.Date - dto.FromDate.Date).TotalDays < AttendanceScheduleHandler.MaxTemporaryDays)
                .WithMessage($"الجدولة المؤقتة حتى {AttendanceScheduleHandler.MaxTemporaryDays} يوماً — للأطول أسند وردية");
            RuleFor(x => x.Note).MaximumLength(300).WithMessage("الملاحظة طويلة جداً");
        }
    }

    public class CreateAttendanceScheduleCommandValidator : AbstractValidator<CreateAttendanceScheduleCommand>
    {
        public CreateAttendanceScheduleCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceScheduleRequestValidator());
    }

    public class UpdateAttendanceScheduleCommandValidator : AbstractValidator<UpdateAttendanceScheduleCommand>
    {
        public UpdateAttendanceScheduleCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceScheduleRequestValidator());
    }

    public class CreateAttendanceTemporaryCommandValidator : AbstractValidator<CreateAttendanceTemporaryCommand>
    {
        public CreateAttendanceTemporaryCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceTemporaryRequestValidator());
    }

    /// <summary>
    /// الجدولة: إسناد وردية لمنطقة أو فرع أو قسم أو مكتب أو شخص، والجدولة المؤقتة، والجدول الفعلي، ومن بلا وردية.
    /// الحد (AttendanceRules.ScheduleLimit): جهات منطقتي وأشخاصها (ScheduleAreaAttendance) أو الكل (ScheduleOrganizationAttendance).
    /// كل تغيير يضع أشخاص الجهة في طابور إعادة الحساب للفترة التي تمسّها (حتى اليوم).
    /// </summary>
    public class AttendanceScheduleHandler :
        IRequestHandler<GetAttendanceSchedulesQuery, List<AttendanceScheduleDto>>,
        IRequestHandler<CreateAttendanceScheduleCommand, AttendanceScheduleDto>,
        IRequestHandler<UpdateAttendanceScheduleCommand, AttendanceScheduleDto>,
        IRequestHandler<DeleteAttendanceScheduleCommand, Unit>,
        IRequestHandler<GetAttendanceTemporariesQuery, List<AttendanceTemporaryDto>>,
        IRequestHandler<CreateAttendanceTemporaryCommand, AttendanceTemporaryDto>,
        IRequestHandler<DeleteAttendanceTemporaryCommand, Unit>,
        IRequestHandler<GetScheduleTargetsQuery, List<ScheduleTargetDto>>,
        IRequestHandler<GetEffectiveScheduleQuery, AttendanceEffectiveDto>,
        IRequestHandler<GetUnscheduledPeopleQuery, List<UnscheduledPersonDto>>
    {
        public const int MaxTemporaryDays = 62;
        public const int MaxEffectiveDays = 62;

        private readonly IAttendanceScheduleService _schedules;
        private readonly IAttendanceShiftService _shifts;
        private readonly IAttendanceCalculationService _calculation;
        private readonly IAttendanceRecalcQueue _recalc;
        private readonly IAttendanceSettings _settings;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public AttendanceScheduleHandler(IAttendanceScheduleService schedules, IAttendanceShiftService shifts,
            IAttendanceCalculationService calculation, IAttendanceRecalcQueue recalc, IAttendanceSettings settings,
            IUserService users, IUserPermissionService permissions)
        {
            _schedules = schedules;
            _shifts = shifts;
            _calculation = calculation;
            _recalc = recalc;
            _settings = settings;
            _users = users;
            _permissions = permissions;
        }

        private async Task<AreaLimit> LimitAsync() => AttendanceRules.ScheduleLimit(await Viewer.CurrentAsync(_users, _permissions));
        private DateTime Today => AttendanceRules.LocalNow(_settings).Date;

        private static ScheduleTargetDto TargetDto(ScheduleTargetInfo info) => new()
        {
            Type = (int)info.Type,
            TypeName = AttendanceShiftRules.TargetName(info.Type),
            Id = info.Id,
            Name = info.Name,
            Detail = info.Detail,
            Missing = !info.Exists
        };

        /// <summary>الجهة موجودة وضمن حدّي</summary>
        private async Task<ScheduleTargetInfo> TargetAsync(AreaLimit limit, AttendanceScheduleTarget type, int id)
        {
            var info = (await _schedules.TargetsAsync([(type, id)]))[(type, id)];
            if (!info.Exists) throw new KeyNotFoundException("الجهة غير موجودة");
            if (!limit.Includes(info.AreaId))
                throw new UnauthorizedAccessException("هذه الجهة خارج منطقتك — لا تملك جدولتها");
            return info;
        }

        private async Task RecalcAsync(AttendanceScheduleTarget type, int targetId, DateTime from, DateTime? to, string reason)
        {
            var users = await _schedules.UsersOfTargetAsync(type, targetId);
            var end = to ?? Today;
            await _recalc.EnqueueManyAsync(users.Select(u => (u, from, end)), reason);
        }

        // ── الإسنادات

        private AttendanceScheduleDto ToDto(AttendanceSchedule s, ScheduleTargetInfo target) => new()
        {
            Id = s.Id,
            Target = TargetDto(target),
            ShiftId = s.ShiftId,
            ShiftName = s.Shift.Name,
            ShiftActive = s.Shift.IsActive,
            FromDate = s.FromDate,
            ToDate = s.ToDate,
            Note = s.Note,
            IsCurrent = s.FromDate <= Today && (s.ToDate == null || s.ToDate >= Today),
            IsPast = s.ToDate != null && s.ToDate < Today,
            CreatedByName = s.CreatedByUser.FullName,
            CreatedAt = s.CreatedAt,
            UpdatedByName = s.UpdatedByUser?.FullName,
            UpdatedAt = s.UpdatedAt,
            RowVersion = s.RowVersion
        };

        public async Task<List<AttendanceScheduleDto>> Handle(GetAttendanceSchedulesQuery request, CancellationToken ct)
        {
            var limit = await LimitAsync();
            var list = await _schedules.GetSchedulesAsync((AttendanceScheduleTarget?)request.TargetType, request.TargetId);
            var targets = await _schedules.TargetsAsync(list.Select(s => (s.TargetType, s.TargetId)));
            // الجهة المحذوفة لا منطقة لها: يراها من يجدول الكل وحده
            return list.Where(s => limit.Includes(targets[(s.TargetType, s.TargetId)].AreaId))
                .Select(s => ToDto(s, targets[(s.TargetType, s.TargetId)])).ToList();
        }

        private async Task<AttendanceScheduleDto> DtoAsync(int id)
        {
            var s = (await _schedules.GetSchedulesAsync(null, null)).First(x => x.Id == id);
            return ToDto(s, (await _schedules.TargetsAsync([(s.TargetType, s.TargetId)]))[(s.TargetType, s.TargetId)]);
        }

        private async Task EnsureShiftAsync(int shiftId, bool mustBeActive)
        {
            var shift = (await _shifts.GetShiftsAsync()).FirstOrDefault(s => s.Id == shiftId) ?? throw new KeyNotFoundException("الوردية غير موجودة");
            if (mustBeActive && !shift.IsActive) throw new InvalidOperationException($"الوردية «{shift.Name}» معطّلة");
        }

        private async Task EnsureNoOverlapAsync(AttendanceScheduleTarget type, int targetId, DateTime from, DateTime? to, int? excludeId)
        {
            if (await _schedules.FindOverlapAsync(type, targetId, from, to, excludeId) is { } other)
                throw new InvalidOperationException(
                    $"للجهة إسناد آخر يتداخل مع هذه الفترة (من {other.FromDate:yyyy-MM-dd}{(other.ToDate is DateTime end ? $" إلى {end:yyyy-MM-dd}" : " بلا نهاية")}) — أنهِه قبل بداية هذا الإسناد");
        }

        public async Task<AttendanceScheduleDto> Handle(CreateAttendanceScheduleCommand request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var limit = AttendanceRules.ScheduleLimit(viewer);
            var dto = request.Dto;
            var type = (AttendanceScheduleTarget)dto.TargetType;
            await TargetAsync(limit, type, dto.TargetId);
            await EnsureShiftAsync(dto.ShiftId, true);
            var from = dto.FromDate.Date;
            var to = dto.ToDate?.Date;
            await EnsureNoOverlapAsync(type, dto.TargetId, from, to, null);

            var schedule = new AttendanceSchedule
            {
                TargetType = type, TargetId = dto.TargetId, ShiftId = dto.ShiftId, FromDate = from, ToDate = to,
                Note = dto.Note?.Trim() ?? string.Empty, CreatedByUserId = viewer.Id, CreatedAt = DateTime.UtcNow
            };
            await _schedules.SaveScheduleAsync(schedule, null);
            await RecalcAsync(type, dto.TargetId, from, to, "إسناد وردية");
            return await DtoAsync(schedule.Id);
        }

        public async Task<AttendanceScheduleDto> Handle(UpdateAttendanceScheduleCommand request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var limit = AttendanceRules.ScheduleLimit(viewer);
            var schedule = await _schedules.GetScheduleAsync(request.Id) ?? throw new KeyNotFoundException("الإسناد غير موجود");
            await TargetAsync(limit, schedule.TargetType, schedule.TargetId);
            var dto = request.Dto;
            // الجهة لا تتغير بالتعديل (احذف الإسناد وأنشئ غيره)
            if ((AttendanceScheduleTarget)dto.TargetType != schedule.TargetType || dto.TargetId != schedule.TargetId)
                throw new InvalidOperationException("لا تتغير جهة الإسناد بالتعديل — احذفه وأنشئ إسناداً للجهة الأخرى");
            await EnsureShiftAsync(dto.ShiftId, dto.ShiftId != schedule.ShiftId);
            var from = dto.FromDate.Date;
            var to = dto.ToDate?.Date;
            await EnsureNoOverlapAsync(schedule.TargetType, schedule.TargetId, from, to, schedule.Id);

            // إعادة الحساب للفترتين معاً (القديمة والجديدة)
            var recalcFrom = from < schedule.FromDate ? from : schedule.FromDate;
            DateTime? recalcTo = to == null || schedule.ToDate == null ? null : (to > schedule.ToDate ? to : schedule.ToDate);

            schedule.ShiftId = dto.ShiftId;
            schedule.FromDate = from;
            schedule.ToDate = to;
            schedule.Note = dto.Note?.Trim() ?? string.Empty;
            schedule.UpdatedByUserId = viewer.Id;
            schedule.UpdatedAt = DateTime.UtcNow;
            await _schedules.SaveScheduleAsync(schedule, dto.RowVersion);
            await RecalcAsync(schedule.TargetType, schedule.TargetId, recalcFrom, recalcTo, "تعديل إسناد وردية");
            return await DtoAsync(schedule.Id);
        }

        public async Task<Unit> Handle(DeleteAttendanceScheduleCommand request, CancellationToken ct)
        {
            var limit = await LimitAsync();
            var schedule = await _schedules.GetScheduleAsync(request.Id) ?? throw new KeyNotFoundException("الإسناد غير موجود");
            var info = (await _schedules.TargetsAsync([(schedule.TargetType, schedule.TargetId)]))[(schedule.TargetType, schedule.TargetId)];
            if (!limit.Includes(info.AreaId)) throw new UnauthorizedAccessException("هذه الجهة خارج منطقتك — لا تملك جدولتها");
            var (type, id, from, to) = (schedule.TargetType, schedule.TargetId, schedule.FromDate, schedule.ToDate);
            await _schedules.DeleteScheduleAsync(schedule);
            await RecalcAsync(type, id, from, to, "حذف إسناد وردية");
            return Unit.Value;
        }

        // ── الجدولة المؤقتة

        private static AttendanceTemporaryDto ToDto(AttendanceTemporarySchedule t, ScheduleTargetInfo target) => new()
        {
            Id = t.Id,
            Target = TargetDto(target),
            FromDate = t.FromDate,
            ToDate = t.ToDate,
            Timetable = t.Timetable == null ? null : Brief(t.Timetable),
            Note = t.Note,
            CreatedByName = t.CreatedByUser.FullName,
            CreatedAt = t.CreatedAt
        };

        private static AttendanceTimetableBriefDto Brief(AttendanceTimetable t) => new()
        {
            Id = t.Id, Name = t.Name, Color = t.Color, Kind = (int)t.Kind,
            StartMinute = t.StartMinute, EndMinute = t.EndMinute, RequiredMinutes = t.RequiredMinutes
        };

        public async Task<List<AttendanceTemporaryDto>> Handle(GetAttendanceTemporariesQuery request, CancellationToken ct)
        {
            var limit = await LimitAsync();
            // المنتهي منذ أكثر من شهر لا يُعرض (يبقى في الحساب)
            var list = await _schedules.GetTemporariesAsync((AttendanceScheduleTarget?)request.TargetType, request.TargetId, Today.AddDays(-31));
            var targets = await _schedules.TargetsAsync(list.Select(t => (t.TargetType, t.TargetId)));
            return list.Where(t => limit.Includes(targets[(t.TargetType, t.TargetId)].AreaId))
                .Select(t => ToDto(t, targets[(t.TargetType, t.TargetId)])).ToList();
        }

        public async Task<AttendanceTemporaryDto> Handle(CreateAttendanceTemporaryCommand request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var limit = AttendanceRules.ScheduleLimit(viewer);
            var dto = request.Dto;
            var type = (AttendanceScheduleTarget)dto.TargetType;
            var target = await TargetAsync(limit, type, dto.TargetId);
            AttendanceTimetable? timetable = null;
            if (dto.TimetableId is int ttId)
            {
                timetable = (await _shifts.GetTimetablesAsync()).FirstOrDefault(t => t.Id == ttId) ?? throw new KeyNotFoundException("الجدول الزمني غير موجود");
                if (!timetable.IsActive) throw new InvalidOperationException($"الجدول «{timetable.Name}» معطّل");
            }
            var from = dto.FromDate.Date;
            var to = dto.ToDate.Date;
            if (await _schedules.FindTemporaryOverlapAsync(type, dto.TargetId, from, to) is { } other)
                throw new InvalidOperationException($"للجهة جدولة مؤقتة أخرى من {other.FromDate:yyyy-MM-dd} إلى {other.ToDate:yyyy-MM-dd} — احذفها أولاً");

            var temp = new AttendanceTemporarySchedule
            {
                TargetType = type, TargetId = dto.TargetId, FromDate = from, ToDate = to, TimetableId = timetable?.Id,
                Note = dto.Note?.Trim() ?? string.Empty, CreatedByUserId = viewer.Id, CreatedAt = DateTime.UtcNow
            };
            await _schedules.AddTemporaryAsync(temp);
            await RecalcAsync(type, dto.TargetId, from, to, "جدولة مؤقتة");
            temp.Timetable = timetable;
            temp.CreatedByUser = viewer.User;
            return ToDto(temp, target);
        }

        public async Task<Unit> Handle(DeleteAttendanceTemporaryCommand request, CancellationToken ct)
        {
            var limit = await LimitAsync();
            var temp = await _schedules.GetTemporaryAsync(request.Id) ?? throw new KeyNotFoundException("الجدولة المؤقتة غير موجودة");
            var info = (await _schedules.TargetsAsync([(temp.TargetType, temp.TargetId)]))[(temp.TargetType, temp.TargetId)];
            if (!limit.Includes(info.AreaId)) throw new UnauthorizedAccessException("هذه الجهة خارج منطقتك — لا تملك جدولتها");
            var (type, id, from, to) = (temp.TargetType, temp.TargetId, temp.FromDate, temp.ToDate);
            await _schedules.DeleteTemporaryAsync(temp);
            await RecalcAsync(type, id, from, to, "حذف جدولة مؤقتة");
            return Unit.Value;
        }

        // ── الجهات والجدول الفعلي

        public async Task<List<ScheduleTargetDto>> Handle(GetScheduleTargetsQuery request, CancellationToken ct)
        {
            if (request.Type is < 1 or > 5) throw new InvalidOperationException("نوع الجهة غير صالح");
            var options = await _schedules.TargetOptionsAsync((AttendanceScheduleTarget)request.Type, request.Query, await LimitAsync());
            return options.Select(TargetDto).ToList();
        }

        public async Task<AttendanceEffectiveDto> Handle(GetEffectiveScheduleQuery request, CancellationToken ct)
        {
            var limit = await LimitAsync();
            var target = await TargetAsync(limit, AttendanceScheduleTarget.Person, request.UserId);
            var from = request.From.Date;
            var to = request.To.Date;
            if (to < from || (to - from).TotalDays >= MaxEffectiveDays)
                throw new InvalidOperationException($"اختر فترة حتى {MaxEffectiveDays} يوماً");

            var days = (await _calculation.EffectiveAsync([request.UserId], from, to)).GetValueOrDefault(request.UserId)
                ?? throw new InvalidOperationException("لا بيانات حضور لهذا الشخص (لا رقم بصمة)");
            var timetables = (await _shifts.GetTimetablesAsync()).ToDictionary(t => t.Id);
            var shifts = (await _shifts.GetShiftsAsync()).ToDictionary(s => s.Id, s => s.Name);
            var person = await _calculation.PersonAsync(request.UserId);
            var sourceNames = await SourceNamesAsync(person);

            return new AttendanceEffectiveDto
            {
                UserId = request.UserId,
                Name = target.Name,
                Number = person?.Number ?? string.Empty,
                Days = days.Select(d => new EffectiveDayDto
                {
                    Date = d.Date,
                    Kind = !d.Active ? "inactive" : d.Plan.Kind switch { DayPlanKind.Work => "work", DayPlanKind.Rest => "rest", _ => "unscheduled" },
                    ShiftName = d.Plan.ShiftId is int sid ? shifts.GetValueOrDefault(sid) : null,
                    Source = d.Plan.Source is AttendanceScheduleTarget src ? sourceNames.GetValueOrDefault(src) : null,
                    Temporary = d.Plan.Temporary,
                    Timetables = d.Plan.TimetableIds.Where(timetables.ContainsKey).Select(id => Brief(timetables[id])).ToList(),
                    HolidayName = d.HolidayName,
                    OnVacation = d.OnVacation
                }).ToList()
            };
        }

        /// <summary>«فرع: الفرع التقني» لكل مستوى جاء منه الجدول</summary>
        private async Task<Dictionary<AttendanceScheduleTarget, string>> SourceNamesAsync(MonitoredPerson? person)
        {
            var result = new Dictionary<AttendanceScheduleTarget, string> { [AttendanceScheduleTarget.Person] = "الشخص نفسه" };
            if (person == null) return result;
            var keys = new List<(AttendanceScheduleTarget, int)>();
            if (person.OfficeId is int o) keys.Add((AttendanceScheduleTarget.Office, o));
            if (person.DepartmentId is int d) keys.Add((AttendanceScheduleTarget.Department, d));
            if (person.BranchId is int b) keys.Add((AttendanceScheduleTarget.Branch, b));
            if (person.AreaId is int a) keys.Add((AttendanceScheduleTarget.Area, a));
            foreach (var (key, info) in await _schedules.TargetsAsync(keys))
                result[key.Type] = $"{AttendanceShiftRules.TargetName(key.Type)}: {info.Name}";
            return result;
        }

        public async Task<List<UnscheduledPersonDto>> Handle(GetUnscheduledPeopleQuery request, CancellationToken ct)
        {
            var limit = await LimitAsync();
            var date = (request.Date ?? Today).Date;
            var people = await _calculation.MonitoredPeopleAsync(limit);
            var plans = await _calculation.EffectiveAsync(people.Select(p => p.UserId).ToList(), date, date);
            return people
                .Where(p => plans.TryGetValue(p.UserId, out var days) && days[0].Active && days[0].Plan.Kind == DayPlanKind.Unscheduled)
                .Select(p => new UnscheduledPersonDto
                {
                    UserId = p.UserId,
                    Name = p.Name,
                    KindName = AttendanceRules.KindName(p.Kind),
                    Number = p.Number,
                    AreaName = p.AreaName,
                    BranchName = p.BranchName,
                    DepartmentName = p.DepartmentName
                }).ToList();
        }
    }
}
