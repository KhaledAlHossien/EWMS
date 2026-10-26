using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Attendance;
using FluentValidation;
using MediatR;

namespace Application.Features.Attendance.Shifts
{
    public record GetAttendanceTimetablesQuery : IRequest<List<AttendanceTimetableDto>>;
    public record CreateAttendanceTimetableCommand(AttendanceTimetableRequestDto Dto) : IRequest<AttendanceTimetableDto>;
    public record UpdateAttendanceTimetableCommand(int Id, AttendanceTimetableRequestDto Dto) : IRequest<AttendanceTimetableDto>;
    public record DeleteAttendanceTimetableCommand(int Id) : IRequest<Unit>;

    public record GetAttendanceShiftsQuery : IRequest<List<AttendanceShiftDto>>;
    public record CreateAttendanceShiftCommand(AttendanceShiftRequestDto Dto) : IRequest<AttendanceShiftDto>;
    public record UpdateAttendanceShiftCommand(int Id, AttendanceShiftRequestDto Dto) : IRequest<AttendanceShiftDto>;
    public record DeleteAttendanceShiftCommand(int Id) : IRequest<Unit>;

    public class AttendanceTimetableRequestValidator : AbstractValidator<AttendanceTimetableRequestDto>
    {
        public AttendanceTimetableRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم الجدول مطلوب").MaximumLength(100).WithMessage("اسم الجدول طويل جداً");
            RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").WithMessage("اللون غير صالح");
            RuleFor(x => x.Kind).InclusiveBetween(1, 2).WithMessage("نوع الجدول غير صالح");
            RuleFor(x => x.LateCounting).InclusiveBetween(1, 2).WithMessage("طريقة عدّ التأخير غير صالحة");
        }
    }

    public class AttendanceShiftRequestValidator : AbstractValidator<AttendanceShiftRequestDto>
    {
        public AttendanceShiftRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم الوردية مطلوب").MaximumLength(100).WithMessage("اسم الوردية طويل جداً");
            RuleFor(x => x.Description).MaximumLength(500).WithMessage("الوصف طويل جداً");
            RuleFor(x => x.CycleUnit).InclusiveBetween(1, 2).WithMessage("وحدة الدورة غير صالحة");
            RuleFor(x => x.CycleLength)
                .Must((dto, length) => dto.CycleUnit == 1
                    ? length is >= 1 and <= AttendanceShiftRules.MaxCycleWeeks
                    : length is >= 1 and <= AttendanceShiftRules.MaxCycleDays)
                .WithMessage($"طول الدورة من 1 إلى {AttendanceShiftRules.MaxCycleWeeks} أسابيع أو من 1 إلى {AttendanceShiftRules.MaxCycleDays} يوماً");
            RuleFor(x => x.StartDate).NotEmpty().WithMessage("تاريخ بداية الدورة مطلوب");
        }
    }

    public class CreateAttendanceTimetableCommandValidator : AbstractValidator<CreateAttendanceTimetableCommand>
    {
        public CreateAttendanceTimetableCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceTimetableRequestValidator());
    }

    public class UpdateAttendanceTimetableCommandValidator : AbstractValidator<UpdateAttendanceTimetableCommand>
    {
        public UpdateAttendanceTimetableCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceTimetableRequestValidator());
    }

    public class CreateAttendanceShiftCommandValidator : AbstractValidator<CreateAttendanceShiftCommand>
    {
        public CreateAttendanceShiftCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceShiftRequestValidator());
    }

    public class UpdateAttendanceShiftCommandValidator : AbstractValidator<UpdateAttendanceShiftCommand>
    {
        public UpdateAttendanceShiftCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceShiftRequestValidator());
    }

    /// <summary>
    /// مكتبة الجداول الزمنية والورديات: مشتركة للمؤسسة (ViewAttendanceShifts / Create / Edit / DeleteAttendanceShift).
    /// المعطّل لا يُختار في إسناد جديد، والإسنادات القائمة تبقى. حذف المستعمل مرفوض (البديل تعطيله).
    /// تعديل جدول أو وردية يُعيد حساب اليوم الحالي لمن يستعملهما (الأيام الماضية تبقى كما حُسبت — قرار الخطة).
    /// </summary>
    public class AttendanceShiftHandler :
        IRequestHandler<GetAttendanceTimetablesQuery, List<AttendanceTimetableDto>>,
        IRequestHandler<CreateAttendanceTimetableCommand, AttendanceTimetableDto>,
        IRequestHandler<UpdateAttendanceTimetableCommand, AttendanceTimetableDto>,
        IRequestHandler<DeleteAttendanceTimetableCommand, Unit>,
        IRequestHandler<GetAttendanceShiftsQuery, List<AttendanceShiftDto>>,
        IRequestHandler<CreateAttendanceShiftCommand, AttendanceShiftDto>,
        IRequestHandler<UpdateAttendanceShiftCommand, AttendanceShiftDto>,
        IRequestHandler<DeleteAttendanceShiftCommand, Unit>
    {
        private readonly IAttendanceShiftService _shifts;
        private readonly IAttendanceRecalcQueue _recalc;
        private readonly IAttendanceSettings _settings;

        public AttendanceShiftHandler(IAttendanceShiftService shifts, IAttendanceRecalcQueue recalc, IAttendanceSettings settings)
        {
            _shifts = shifts;
            _recalc = recalc;
            _settings = settings;
        }

        private Task RecalcTodayAsync(string reason)
        {
            var today = AttendanceRules.LocalNow(_settings).Date;
            return _recalc.EnqueueAsync(null, today, today, reason);
        }

        // ── الجداول الزمنية

        private static AttendanceTimetableDto ToDto(AttendanceTimetable t, int used) => new()
        {
            Id = t.Id,
            Name = t.Name,
            Kind = (int)t.Kind,
            KindName = AttendanceShiftRules.KindName(t.Kind),
            Color = t.Color,
            StartMinute = t.StartMinute,
            EndMinute = t.EndMinute,
            Overnight = AttendanceShiftRules.IsOvernight(t.StartMinute, t.EndMinute),
            SpanMinutes = AttendanceShiftRules.SpanMinutes(t.StartMinute, t.EndMinute),
            CheckInBeforeMinutes = t.CheckInBeforeMinutes,
            CheckInAfterMinutes = t.CheckInAfterMinutes,
            CheckOutBeforeMinutes = t.CheckOutBeforeMinutes,
            CheckOutAfterMinutes = t.CheckOutAfterMinutes,
            LateGraceMinutes = t.LateGraceMinutes,
            EarlyLeaveGraceMinutes = t.EarlyLeaveGraceMinutes,
            LateCounting = (int)t.LateCounting,
            RequireCheckIn = t.RequireCheckIn,
            RequireCheckOut = t.RequireCheckOut,
            BreakMinutes = t.BreakMinutes,
            RequiredMinutes = t.RequiredMinutes,
            IsActive = t.IsActive,
            UsedByShifts = used,
            UpdatedAt = t.UpdatedAt,
            RowVersion = t.RowVersion
        };

        public async Task<List<AttendanceTimetableDto>> Handle(GetAttendanceTimetablesQuery request, CancellationToken ct)
        {
            var usage = await _shifts.TimetableUsageAsync();
            return (await _shifts.GetTimetablesAsync())
                .OrderByDescending(t => t.IsActive).ThenBy(t => t.StartMinute).ThenBy(t => t.Name)
                .Select(t => ToDto(t, usage.GetValueOrDefault(t.Id))).ToList();
        }

        private static void Apply(AttendanceTimetable t, AttendanceTimetableRequestDto dto)
        {
            t.Name = dto.Name.Trim();
            t.Kind = (AttendanceTimetableKind)dto.Kind;
            t.Color = dto.Color.ToUpperInvariant();
            t.StartMinute = dto.StartMinute;
            t.EndMinute = dto.EndMinute;
            t.CheckInBeforeMinutes = dto.CheckInBeforeMinutes;
            t.CheckInAfterMinutes = dto.CheckInAfterMinutes;
            t.CheckOutBeforeMinutes = dto.CheckOutBeforeMinutes;
            t.CheckOutAfterMinutes = dto.CheckOutAfterMinutes;
            t.LateGraceMinutes = dto.LateGraceMinutes;
            t.EarlyLeaveGraceMinutes = dto.EarlyLeaveGraceMinutes;
            t.LateCounting = (AttendanceLateCounting)dto.LateCounting;
            t.RequireCheckIn = dto.RequireCheckIn;
            t.RequireCheckOut = dto.RequireCheckOut;
            t.BreakMinutes = dto.BreakMinutes;
            t.RequiredMinutes = dto.RequiredMinutes;
            t.IsActive = dto.IsActive;
            AttendanceShiftRules.Validate(t);
        }

        private async Task<AttendanceTimetableDto> TimetableDtoAsync(int id)
        {
            var usage = await _shifts.TimetableUsageAsync();
            var t = (await _shifts.GetTimetablesAsync()).First(x => x.Id == id);
            return ToDto(t, usage.GetValueOrDefault(id));
        }

        public async Task<AttendanceTimetableDto> Handle(CreateAttendanceTimetableCommand request, CancellationToken ct)
        {
            if (await _shifts.TimetableNameExistsAsync(request.Dto.Name.Trim()))
                throw new InvalidOperationException("يوجد جدول زمني بنفس الاسم");
            var now = DateTime.UtcNow;
            var t = new AttendanceTimetable { CreatedAt = now, UpdatedAt = now };
            Apply(t, request.Dto);
            await _shifts.SaveTimetableAsync(t, null);
            return await TimetableDtoAsync(t.Id);
        }

        public async Task<AttendanceTimetableDto> Handle(UpdateAttendanceTimetableCommand request, CancellationToken ct)
        {
            var t = await _shifts.GetTimetableAsync(request.Id) ?? throw new KeyNotFoundException("الجدول الزمني غير موجود");
            if (await _shifts.TimetableNameExistsAsync(request.Dto.Name.Trim(), request.Id))
                throw new InvalidOperationException("يوجد جدول زمني آخر بنفس الاسم");
            Apply(t, request.Dto);
            t.UpdatedAt = DateTime.UtcNow;
            await _shifts.SaveTimetableAsync(t, request.Dto.RowVersion);
            await RecalcTodayAsync("تعديل جدول زمني");
            return await TimetableDtoAsync(t.Id);
        }

        public async Task<Unit> Handle(DeleteAttendanceTimetableCommand request, CancellationToken ct)
        {
            var t = await _shifts.GetTimetableAsync(request.Id) ?? throw new KeyNotFoundException("الجدول الزمني غير موجود");
            if ((await _shifts.TimetableUsageAsync()).GetValueOrDefault(t.Id) > 0)
                throw new InvalidOperationException("هذا الجدول مستعمل في وردية أو جدولة مؤقتة — عطّله بدلاً من حذفه");
            await _shifts.DeleteTimetableAsync(t);
            return Unit.Value;
        }

        // ── الورديات

        private static AttendanceShiftDto ToDto(AttendanceShift s, int used) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            CycleUnit = (int)s.CycleUnit,
            CycleLength = s.CycleLength,
            CycleDays = AttendanceShiftRules.CycleDays(s.CycleUnit, s.CycleLength),
            StartDate = s.StartDate,
            IsActive = s.IsActive,
            Days = s.Days.GroupBy(d => d.DayIndex).OrderBy(g => g.Key).Select(g => new AttendanceShiftDayDto
            {
                DayIndex = g.Key,
                Timetables = g.Select(d => d.Timetable).OrderBy(t => t.StartMinute).Select(t => new AttendanceTimetableBriefDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Color = t.Color,
                    Kind = (int)t.Kind,
                    StartMinute = t.StartMinute,
                    EndMinute = t.EndMinute,
                    RequiredMinutes = t.RequiredMinutes
                }).ToList()
            }).ToList(),
            UsedBySchedules = used,
            UpdatedAt = s.UpdatedAt,
            RowVersion = s.RowVersion
        };

        public async Task<List<AttendanceShiftDto>> Handle(GetAttendanceShiftsQuery request, CancellationToken ct)
        {
            var usage = await _shifts.ShiftUsageAsync();
            return (await _shifts.GetShiftsAsync())
                .OrderByDescending(s => s.IsActive).ThenBy(s => s.Name)
                .Select(s => ToDto(s, usage.GetValueOrDefault(s.Id))).ToList();
        }

        private async Task<AttendanceShiftDto> ShiftDtoAsync(int id)
        {
            var usage = await _shifts.ShiftUsageAsync();
            return ToDto((await _shifts.GetShiftsAsync()).First(s => s.Id == id), usage.GetValueOrDefault(id));
        }

        /// <summary>أيام الوردية: داخل الدورة، حتى 4 جداول لليوم، جداول موجودة لا تتداخل أوقاتها في اليوم نفسه</summary>
        private async Task<List<(int DayIndex, int TimetableId)>> DaysAsync(AttendanceShiftRequestDto dto, AttendanceShift? existing)
        {
            var cycleDays = AttendanceShiftRules.CycleDays((AttendanceCycleUnit)dto.CycleUnit, dto.CycleLength);
            var timetables = (await _shifts.GetTimetablesAsync()).ToDictionary(t => t.Id);
            var used = existing?.Days.Select(d => d.TimetableId).ToHashSet() ?? [];
            var result = new List<(int, int)>();
            foreach (var day in dto.Days.GroupBy(d => d.DayIndex))
            {
                if (day.Key < 0 || day.Key >= cycleDays) throw new InvalidOperationException("يوم خارج الدورة");
                var ids = day.SelectMany(d => d.TimetableIds).Distinct().ToList();
                if (ids.Count > AttendanceShiftRules.MaxTimetablesPerDay)
                    throw new InvalidOperationException($"اليوم الواحد يحمل {AttendanceShiftRules.MaxTimetablesPerDay} جداول على الأكثر");
                var list = new List<AttendanceTimetable>();
                foreach (var id in ids)
                {
                    if (!timetables.TryGetValue(id, out var t)) throw new KeyNotFoundException("أحد الجداول المختارة غير موجود");
                    // المعطّل يبقى حيث كان، ولا يُضاف إلى وردية من جديد
                    if (!t.IsActive && !used.Contains(id)) throw new InvalidOperationException($"الجدول «{t.Name}» معطّل");
                    list.Add(t);
                }
                var ordered = list.OrderBy(t => t.StartMinute).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    var prevEnd = ordered[i - 1].StartMinute + AttendanceShiftRules.SpanMinutes(ordered[i - 1].StartMinute, ordered[i - 1].EndMinute);
                    if (ordered[i].StartMinute < prevEnd)
                        throw new InvalidOperationException($"الجدولان «{ordered[i - 1].Name}» و«{ordered[i].Name}» متداخلان في اليوم نفسه");
                }
                result.AddRange(ids.Select(id => (day.Key, id)));
            }
            return result;
        }

        private static void Apply(AttendanceShift s, AttendanceShiftRequestDto dto)
        {
            s.Name = dto.Name.Trim();
            s.Description = dto.Description?.Trim() ?? string.Empty;
            s.CycleUnit = (AttendanceCycleUnit)dto.CycleUnit;
            s.CycleLength = dto.CycleLength;
            s.StartDate = dto.StartDate.Date;
            s.IsActive = dto.IsActive;
        }

        public async Task<AttendanceShiftDto> Handle(CreateAttendanceShiftCommand request, CancellationToken ct)
        {
            if (await _shifts.ShiftNameExistsAsync(request.Dto.Name.Trim()))
                throw new InvalidOperationException("توجد وردية بنفس الاسم");
            var now = DateTime.UtcNow;
            var s = new AttendanceShift { CreatedAt = now, UpdatedAt = now };
            Apply(s, request.Dto);
            await _shifts.SaveShiftAsync(s, null, await DaysAsync(request.Dto, null));
            return await ShiftDtoAsync(s.Id);
        }

        public async Task<AttendanceShiftDto> Handle(UpdateAttendanceShiftCommand request, CancellationToken ct)
        {
            var s = await _shifts.GetShiftAsync(request.Id) ?? throw new KeyNotFoundException("الوردية غير موجودة");
            if (await _shifts.ShiftNameExistsAsync(request.Dto.Name.Trim(), request.Id))
                throw new InvalidOperationException("توجد وردية أخرى بنفس الاسم");
            var days = await DaysAsync(request.Dto, s);
            Apply(s, request.Dto);
            s.UpdatedAt = DateTime.UtcNow;
            await _shifts.SaveShiftAsync(s, request.Dto.RowVersion, days);
            await RecalcTodayAsync("تعديل وردية");
            return await ShiftDtoAsync(s.Id);
        }

        public async Task<Unit> Handle(DeleteAttendanceShiftCommand request, CancellationToken ct)
        {
            var s = await _shifts.GetShiftAsync(request.Id) ?? throw new KeyNotFoundException("الوردية غير موجودة");
            if ((await _shifts.ShiftUsageAsync()).GetValueOrDefault(s.Id) > 0)
                throw new InvalidOperationException("هذه الوردية مسندة لجهة أو شخص — عطّلها أو احذف إسناداتها أولاً");
            await _shifts.DeleteShiftAsync(s);
            return Unit.Value;
        }
    }
}
