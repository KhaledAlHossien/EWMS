using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Attendance;
using FluentValidation;
using MediatR;

namespace Application.Features.Attendance.Shifts
{
    public record GetAttendanceCorrectionsQuery(int UserId, DateTime From, DateTime To) : IRequest<List<AttendanceCorrectionDto>>;
    public record CreateAttendanceCorrectionCommand(AttendanceCorrectionRequestDto Dto) : IRequest<AttendanceCorrectionDto>;
    public record CancelAttendanceCorrectionCommand(long Id, string Reason) : IRequest<AttendanceCorrectionDto>;

    public class CreateAttendanceCorrectionCommandValidator : AbstractValidator<CreateAttendanceCorrectionCommand>
    {
        public CreateAttendanceCorrectionCommandValidator()
        {
            RuleFor(x => x.Dto.UserId).GreaterThan(0).WithMessage("اختر الشخص");
            RuleFor(x => x.Dto.Kind).InclusiveBetween(1, 3).WithMessage("نوع التصحيح غير صالح");
            RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("السبب إلزامي").MaximumLength(500).WithMessage("السبب طويل جداً");
        }
    }

    public class CancelAttendanceCorrectionCommandValidator : AbstractValidator<CancelAttendanceCorrectionCommand>
    {
        public CancelAttendanceCorrectionCommandValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("سبب الإلغاء إلزامي").MaximumLength(500).WithMessage("السبب طويل جداً");
    }

    /// <summary>
    /// التصحيح اليدوي المباشر (بلا طلبات ولا موافقات — قرار المستخدم): بصمة يدوية، تجاهل بصمة، تبرير يوم أو فترة.
    /// الحد: أشخاص منطقتي (CorrectAreaAttendance) أو الكل (CorrectOrganizationAttendance). لا يُحذف تصحيح: يُلغى بسبب ويبقى في السجل.
    /// كل تصحيح أو إلغاء يضع أيامه (واليوم الذي قبلها، للجدول الليلي) في طابور إعادة الحساب.
    /// </summary>
    public class AttendanceCorrectionHandler :
        IRequestHandler<GetAttendanceCorrectionsQuery, List<AttendanceCorrectionDto>>,
        IRequestHandler<CreateAttendanceCorrectionCommand, AttendanceCorrectionDto>,
        IRequestHandler<CancelAttendanceCorrectionCommand, AttendanceCorrectionDto>
    {
        public const int MaxDaysBack = 400;
        public const int MaxExcuseDays = 62;

        private readonly IAttendanceCorrectionService _corrections;
        private readonly IAttendanceCalculationService _calculation;
        private readonly IAttendanceRecalcQueue _recalc;
        private readonly IAttendanceSettings _settings;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public AttendanceCorrectionHandler(IAttendanceCorrectionService corrections, IAttendanceCalculationService calculation,
            IAttendanceRecalcQueue recalc, IAttendanceSettings settings, IUserService users, IUserPermissionService permissions)
        {
            _corrections = corrections;
            _calculation = calculation;
            _recalc = recalc;
            _settings = settings;
            _users = users;
            _permissions = permissions;
        }

        private DateTime Now => AttendanceRules.LocalNow(_settings);

        /// <summary>الشخص له بيانات حضور ويقع في حدّ تصحيحي</summary>
        private async Task<Viewer> EnsureAsync(int userId)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var person = await _calculation.PersonAsync(userId) ?? throw new InvalidOperationException("لا بيانات حضور لهذا الشخص (لا رقم بصمة)");
            AttendanceRules.EnsureManage(AttendanceRules.CorrectLimit(viewer), person.AreaId, "هذا الشخص خارج منطقتك — لا تملك تصحيح حضوره");
            return viewer;
        }

        public static string KindName(AttendanceCorrectionKind kind) => kind switch
        {
            AttendanceCorrectionKind.AddPunch => "بصمة يدوية",
            AttendanceCorrectionKind.IgnorePunch => "تجاهل بصمة",
            AttendanceCorrectionKind.Excuse => "تبرير",
            _ => ""
        };

        private static string Summary(AttendanceCorrection c) => c.Kind switch
        {
            AttendanceCorrectionKind.AddPunch => $"بصمة يدوية {c.PunchTime:yyyy-MM-dd HH:mm}",
            AttendanceCorrectionKind.IgnorePunch => $"تجاهل بصمة {c.PunchTime:yyyy-MM-dd HH:mm}",
            _ => (c.ExcuseKind is AttendanceExcuseKind kind ? AttendanceShiftRules.ExcuseName(kind) : "تبرير") + " " + (c.WholeDays
                ? (c.FromDate == c.ToDate ? $"يوم {c.FromDate:yyyy-MM-dd}" : $"من {c.FromDate:yyyy-MM-dd} إلى {c.ToDate:yyyy-MM-dd}")
                : $"{c.FromAt:yyyy-MM-dd HH:mm} – {c.ToAt:HH:mm}")
        };

        public static AttendanceCorrectionDto ToDto(AttendanceCorrection c) => new()
        {
            Id = c.Id,
            UserId = c.UserId,
            Kind = (int)c.Kind,
            KindName = KindName(c.Kind),
            Summary = Summary(c),
            FromDate = c.FromDate,
            ToDate = c.ToDate,
            PunchTime = c.PunchTime,
            PunchId = c.PunchId,
            ExcuseKind = (int?)c.ExcuseKind,
            WholeDays = c.WholeDays,
            FromAt = c.FromAt,
            ToAt = c.ToAt,
            Reason = c.Reason,
            CreatedByName = c.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = c.CreatedAt,
            Cancelled = c.CancelledAt != null,
            CancelledByName = c.CancelledByUser?.FullName,
            CancelledAt = c.CancelledAt,
            CancelReason = c.CancelReason
        };

        public async Task<List<AttendanceCorrectionDto>> Handle(GetAttendanceCorrectionsQuery request, CancellationToken ct)
        {
            await EnsureAsync(request.UserId);
            if ((request.To.Date - request.From.Date).TotalDays > 62) throw new InvalidOperationException("اختر فترة حتى 62 يوماً");
            return (await _corrections.ListAsync(request.UserId, request.From, request.To)).Select(ToDto).ToList();
        }

        public async Task<AttendanceCorrectionDto> Handle(CreateAttendanceCorrectionCommand request, CancellationToken ct)
        {
            var dto = request.Dto;
            var viewer = await EnsureAsync(dto.UserId);
            var now = Now;
            var oldest = now.Date.AddDays(-MaxDaysBack);
            var c = new AttendanceCorrection
            {
                UserId = dto.UserId,
                Kind = (AttendanceCorrectionKind)dto.Kind,
                Reason = dto.Reason.Trim(),
                CreatedByUserId = viewer.Id,
                CreatedAt = DateTime.UtcNow
            };

            switch (c.Kind)
            {
                case AttendanceCorrectionKind.AddPunch:
                    var time = dto.PunchTime ?? throw new InvalidOperationException("أدخل وقت البصمة");
                    time = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0);
                    if (time > now) throw new InvalidOperationException("لا تُضاف بصمة في المستقبل");
                    if (time < oldest) throw new InvalidOperationException($"البصمة أقدم من {MaxDaysBack} يوماً");
                    c.PunchTime = time;
                    c.FromDate = c.ToDate = time.Date;
                    break;

                case AttendanceCorrectionKind.IgnorePunch:
                    var id = dto.PunchId ?? throw new InvalidOperationException("اختر البصمة");
                    var punch = await _corrections.PunchAsync(id) ?? throw new KeyNotFoundException("البصمة غير موجودة");
                    if (punch.UserId != dto.UserId) throw new InvalidOperationException("البصمة ليست لهذا الشخص");
                    if (await _corrections.IgnoredAsync(id)) throw new InvalidOperationException("هذه البصمة متجاهلة من قبل");
                    c.PunchId = id;
                    c.PunchTime = punch.Time;
                    c.FromDate = c.ToDate = punch.Time.Date;
                    break;

                default:
                    if (dto.ExcuseKind is not (>= 1 and <= 4)) throw new InvalidOperationException("اختر نوع التبرير");
                    c.ExcuseKind = (AttendanceExcuseKind)dto.ExcuseKind;
                    c.WholeDays = dto.WholeDays;
                    if (dto.WholeDays)
                    {
                        var from = (dto.FromDate ?? throw new InvalidOperationException("أدخل تاريخ البداية")).Date;
                        var to = (dto.ToDate ?? from).Date;
                        if (to < from) throw new InvalidOperationException("تاريخ النهاية قبل البداية");
                        if ((to - from).TotalDays >= MaxExcuseDays) throw new InvalidOperationException($"التبرير حتى {MaxExcuseDays} يوماً");
                        if (from < oldest) throw new InvalidOperationException($"التبرير أقدم من {MaxDaysBack} يوماً");
                        c.FromDate = from;
                        c.ToDate = to;
                    }
                    else
                    {
                        var fromAt = dto.FromAt ?? throw new InvalidOperationException("أدخل بداية الفترة");
                        var toAt = dto.ToAt ?? throw new InvalidOperationException("أدخل نهاية الفترة");
                        if (toAt <= fromAt) throw new InvalidOperationException("نهاية الفترة قبل بدايتها");
                        if ((toAt - fromAt).TotalHours > 24) throw new InvalidOperationException("فترة التبرير الجزئي حتى 24 ساعة — للأطول اختر أياماً كاملة");
                        if (fromAt < oldest) throw new InvalidOperationException($"التبرير أقدم من {MaxDaysBack} يوماً");
                        c.FromAt = fromAt;
                        c.ToAt = toAt;
                        c.FromDate = fromAt.Date;
                        c.ToDate = toAt.Date;
                    }
                    break;
            }

            await _corrections.AddAsync(c);
            await _recalc.EnqueueAsync(c.UserId, c.FromDate.AddDays(-1), c.ToDate, "تصحيح حضور");
            c.CreatedByUser = viewer.User;
            return ToDto(c);
        }

        public async Task<AttendanceCorrectionDto> Handle(CancelAttendanceCorrectionCommand request, CancellationToken ct)
        {
            var c = await _corrections.GetAsync(request.Id) ?? throw new KeyNotFoundException("التصحيح غير موجود");
            var viewer = await EnsureAsync(c.UserId);
            if (c.CancelledAt != null) throw new InvalidOperationException("هذا التصحيح ملغى من قبل");
            c.CancelledAt = DateTime.UtcNow;
            c.CancelledByUserId = viewer.Id;
            c.CancelReason = request.Reason.Trim();
            await _corrections.SaveAsync();
            await _recalc.EnqueueAsync(c.UserId, c.FromDate.AddDays(-1), c.ToDate, "إلغاء تصحيح حضور");
            c.CancelledByUser = viewer.User;
            return ToDto(c);
        }
    }
}
