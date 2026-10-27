using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Attendance;
using MediatR;

namespace Application.Features.Attendance.Shifts
{
    public record GetAttendanceDailyQuery(DateTime? Date, int? AreaId, int? BranchId, int? DepartmentId, string? Status, string? Query,
        int Page, int PageSize) : IRequest<AttendanceDailyDto>;
    public record GetAttendanceSheetQuery(int UserId, string? Month) : IRequest<AttendanceSheetDto>;
    public record GetMyAttendanceSheetQuery(string? Month) : IRequest<AttendanceSheetDto>;
    public record RecalculateAttendanceCommand(AttendanceRecalculateRequestDto Dto) : IRequest<int>;

    /// <summary>
    /// نتائج الحساب: «الحضور اليومي» و«كشف الشخص» بحدود صلاحيات العرض الحالية (AttendanceRules.ViewScope)،
    /// و«دوامي» بـ ViewMyAttendance، وإعادة الحساب اليدوية بحد التصحيح (منطقتي أو الكل).
    /// </summary>
    public class AttendanceDayHandler :
        IRequestHandler<GetAttendanceDailyQuery, AttendanceDailyDto>,
        IRequestHandler<GetAttendanceSheetQuery, AttendanceSheetDto>,
        IRequestHandler<GetMyAttendanceSheetQuery, AttendanceSheetDto>,
        IRequestHandler<RecalculateAttendanceCommand, int>
    {
        public const int MaxRecalcDays = 400;

        private readonly IAttendanceDayService _days;
        private readonly IAttendanceCalculationService _calculation;
        private readonly IAttendanceRecalcQueue _recalc;
        private readonly IAttendanceSettings _settings;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public AttendanceDayHandler(IAttendanceDayService days, IAttendanceCalculationService calculation, IAttendanceRecalcQueue recalc,
            IAttendanceSettings settings, IUserService users, IUserPermissionService permissions)
        {
            _days = days;
            _calculation = calculation;
            _recalc = recalc;
            _settings = settings;
            _users = users;
            _permissions = permissions;
        }

        private DateTime Today => AttendanceRules.LocalNow(_settings).Date;

        public static AttendanceDayPartDto PartDto(AttendanceDayPart p) => new()
        {
            TimetableName = p.TimetableName,
            Kind = (int)p.Kind,
            PlannedStart = p.PlannedStart,
            PlannedEnd = p.PlannedEnd,
            CheckIn = p.CheckIn,
            CheckOut = p.CheckOut,
            Status = (int)p.Status,
            StatusName = AttendanceShiftRules.StatusName(p.Status),
            LateMinutes = p.LateMinutes,
            EarlyMinutes = p.EarlyMinutes,
            ShortMinutes = p.ShortMinutes,
            WorkMinutes = p.WorkMinutes,
            RequiredMinutes = p.RequiredMinutes
        };

        public async Task<AttendanceDailyDto> Handle(GetAttendanceDailyQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var scope = AttendanceRules.ViewScope(viewer);
            var date = (request.Date ?? Today).Date;
            var page = Math.Max(1, request.Page);
            var size = Math.Clamp(request.PageSize, 1, 200);
            var (items, total, c) = await _days.DailyAsync(scope,
                new DailyFilter(date, request.AreaId, request.BranchId, request.DepartmentId, request.Status, request.Query), page, size);

            return new AttendanceDailyDto
            {
                Date = date,
                Counts = new AttendanceDailyCountsDto
                {
                    Total = c.Total, Present = c.Present, Late = c.Late, Early = c.Early, Absent = c.Absent, Incomplete = c.Incomplete,
                    Excused = c.Excused, Pending = c.Pending, Vacation = c.Vacation, Holiday = c.Holiday, Rest = c.Rest
                },
                Rows = new PagedResultDto<AttendanceDailyRowDto>
                {
                    Items = items.Select(r => new AttendanceDailyRowDto
                    {
                        UserId = r.Day.UserId,
                        Name = r.Name,
                        KindName = AttendanceRules.KindName(r.Kind),
                        Number = r.Number,
                        BranchName = r.BranchName,
                        DepartmentName = r.DepartmentName,
                        ShiftName = r.ShiftName,
                        Type = (int)r.Day.Type,
                        Status = (int)r.Day.Status,
                        StatusName = AttendanceShiftRules.StatusName(r.Day.Status),
                        FirstIn = r.Day.FirstIn,
                        LastOut = r.Day.LastOut,
                        LateMinutes = r.Day.LateMinutes,
                        EarlyMinutes = r.Day.EarlyMinutes,
                        ShortMinutes = r.Day.ShortMinutes,
                        WorkMinutes = r.Day.WorkMinutes,
                        RequiredMinutes = r.Day.RequiredMinutes,
                        HasExcuse = r.Day.HasExcuse,
                        Parts = r.Day.Parts.OrderBy(p => p.Order).Select(PartDto).ToList()
                    }).ToList(),
                    TotalCount = total,
                    Page = page,
                    PageSize = size
                },
                CanCorrect = !AttendanceRules.CorrectLimit(viewer).IsNothing
            };
        }

        public async Task<AttendanceSheetDto> Handle(GetAttendanceSheetQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            if (!await _days.PersonInScopeAsync(AttendanceRules.ViewScope(viewer), request.UserId))
                throw new UnauthorizedAccessException("لا تملك عرض حضور هذا الشخص");
            return await SheetAsync(viewer, request.UserId, request.Month);
        }

        public async Task<AttendanceSheetDto> Handle(GetMyAttendanceSheetQuery request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            return await SheetAsync(viewer, viewer.Id, request.Month);
        }

        /// <summary>«yyyy-MM» ← أول الشهر (الفارغ = الشهر الحالي)</summary>
        private DateTime MonthStart(string? month)
        {
            if (string.IsNullOrWhiteSpace(month)) return new DateTime(Today.Year, Today.Month, 1);
            if (!DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var start))
                throw new InvalidOperationException("الشهر غير صالح");
            return start;
        }

        private async Task<AttendanceSheetDto> SheetAsync(Viewer viewer, int userId, string? month)
        {
            var person = await _calculation.PersonAsync(userId) ?? throw new InvalidOperationException("لا بيانات حضور لهذا الشخص (لا رقم بصمة)");
            var from = MonthStart(month);
            var to = from.AddMonths(1).AddDays(-1);
            var days = (await _days.DaysAsync(userId, from, to)).ToDictionary(d => d.Date);
            var punches = await _days.PunchesAsync(userId, from, to);
            var shifts = await _days.ShiftNamesAsync();
            var today = Today;

            var list = new List<AttendanceSheetDayDto>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                var dto = new AttendanceSheetDayDto
                {
                    Date = d,
                    Future = d > today,
                    Punches = punches.Where(p => p.Time.Date == d).Select(p =>
                    {
                        var (key, name) = p.Manual ? ("manual", "يدوية") : AttendanceRules.Verify(p.VerifyType);
                        return new SheetPunchDto
                        {
                            PunchId = p.PunchId, CorrectionId = p.CorrectionId, Time = p.Time, DeviceName = p.DeviceName,
                            Verify = key, VerifyName = name, Manual = p.Manual, Ignored = p.Ignored
                        };
                    }).ToList()
                };
                if (days.TryGetValue(d, out var day))
                {
                    dto.Calculated = true;
                    dto.Type = (int)day.Type;
                    dto.Status = (int)day.Status;
                    dto.StatusName = AttendanceShiftRules.StatusName(day.Status);
                    dto.ShiftName = day.ShiftId is int sid ? shifts.GetValueOrDefault(sid) : null;
                    dto.FirstIn = day.FirstIn;
                    dto.LastOut = day.LastOut;
                    dto.LateMinutes = day.LateMinutes;
                    dto.EarlyMinutes = day.EarlyMinutes;
                    dto.ShortMinutes = day.ShortMinutes;
                    dto.WorkMinutes = day.WorkMinutes;
                    dto.RequiredMinutes = day.RequiredMinutes;
                    dto.HasExcuse = day.HasExcuse;
                    dto.Parts = day.Parts.OrderBy(p => p.Order).Select(PartDto).ToList();
                }
                else
                {
                    dto.StatusName = d > today ? "" : person.ActiveOn(d) ? "غير مجدول" : "خارج فترة العمل";
                }
                list.Add(dto);
            }

            var all = days.Values.ToList();
            var work = all.Where(d => d.Type == AttendanceDayType.Work).ToList();
            var correct = AttendanceRules.CorrectLimit(viewer);
            return new AttendanceSheetDto
            {
                UserId = userId,
                Name = person.Name,
                Number = person.Number,
                KindName = AttendanceRules.KindName(person.Kind),
                BranchName = person.BranchName,
                DepartmentName = person.DepartmentName,
                Month = from.ToString("yyyy-MM"),
                Days = list,
                Totals = new AttendanceSheetTotalsDto
                {
                    WorkDays = work.Count,
                    Present = work.Count(d => d.Status == AttendanceDayStatus.Present),
                    LateDays = work.Count(d => d.LateMinutes > 0),
                    LateMinutes = work.Sum(d => d.LateMinutes),
                    EarlyMinutes = work.Sum(d => d.EarlyMinutes),
                    ShortMinutes = work.Sum(d => d.ShortMinutes),
                    Absent = work.Count(d => d.Status == AttendanceDayStatus.Absent),
                    Incomplete = work.Count(d => d.Status == AttendanceDayStatus.Incomplete),
                    Excused = work.Count(d => d.Status == AttendanceDayStatus.Excused),
                    Vacation = all.Count(d => d.Status == AttendanceDayStatus.Vacation),
                    Holiday = all.Count(d => d.Status == AttendanceDayStatus.Holiday),
                    Rest = all.Count(d => d.Status == AttendanceDayStatus.Rest),
                    WorkMinutes = work.Sum(d => d.WorkMinutes),
                    RequiredMinutes = work.Sum(d => d.RequiredMinutes)
                },
                CanCorrect = correct.Includes(person.AreaId)
            };
        }

        public async Task<int> Handle(RecalculateAttendanceCommand request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var limit = AttendanceRules.CorrectLimit(viewer);
            if (limit.IsNothing) throw new UnauthorizedAccessException("لا تملك إعادة حساب الحضور");
            var from = request.Dto.From.Date;
            var to = request.Dto.To.Date > Today ? Today : request.Dto.To.Date;
            if (to < from) throw new InvalidOperationException("الفترة غير صالحة (ما بعد اليوم لا يُحسب)");
            if ((to - from).TotalDays >= MaxRecalcDays) throw new InvalidOperationException($"اختر فترة حتى {MaxRecalcDays} يوماً");

            if (request.Dto.UserId is int userId)
            {
                var person = await _calculation.PersonAsync(userId) ?? throw new InvalidOperationException("لا بيانات حضور لهذا الشخص");
                AttendanceRules.EnsureManage(limit, person.AreaId);
                await _recalc.EnqueueAsync(userId, from, to, "إعادة حساب يدوية");
                return 1;
            }
            if (limit.All)
            {
                await _recalc.EnqueueAsync(null, from, to, "إعادة حساب يدوية");
                return (await _calculation.MonitoredPeopleAsync(limit)).Count;
            }
            var people = await _calculation.MonitoredPeopleAsync(limit);
            await _recalc.EnqueueManyAsync(people.Select(p => (p.UserId, from, to)), "إعادة حساب يدوية");
            return people.Count;
        }
    }
}
