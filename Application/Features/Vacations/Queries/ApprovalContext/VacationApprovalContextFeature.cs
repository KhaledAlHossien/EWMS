using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.Vacations.Queries.ApprovalContext
{
    public record GetVacationApprovalContextQuery(int Id) : IRequest<VacationApprovalContextDto>;

    /// <summary>
    /// «سجل الموظف» في بطاقة الطلب قبل القرار (قرار المستخدم 2026-10-04). لا صلاحية جديدة: يُسمح فقط لمن يستطيع
    /// اتخاذ القرار على هذا الطلب في مرحلته الحالية (VacationAccess.EnsureCanDecide — نفس فحوص الموافقة).
    /// </summary>
    public class GetVacationApprovalContextQueryHandler : IRequestHandler<GetVacationApprovalContextQuery, VacationApprovalContextDto>
    {
        private readonly IVacationService _service;
        private readonly IVacationTypeService _types;
        private readonly IPublicHolidayService _holidays;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetVacationApprovalContextQueryHandler(
            IVacationService service,
            IVacationTypeService types,
            IPublicHolidayService holidays,
            IUserService userService,
            IUserPermissionService permissions)
        {
            _service = service;
            _types = types;
            _holidays = holidays;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<VacationApprovalContextDto> Handle(GetVacationApprovalContextQuery request, CancellationToken ct)
        {
            var vacation = await _service.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            VacationAccess.EnsureCanDecide(viewer, vacation);

            var today = DateTime.Today;
            var start = vacation.StartVac.Date;
            var end = vacation.EndVac.Date;
            var result = new VacationApprovalContextDto { MaxPaidDaysPerMonth = VacationRules.MaxPaidVacationDaysPerMonth };

            // 1) آخر إجازة معتمدة بدأت حتى اليوم
            var last = await _service.GetLastTakenAsync(vacation.UserId, today);
            if (last != null)
            {
                result.LastVacation = Item(last);
                result.DaysSinceLastVacation = last.EndVac.Date < today ? (today - last.EndVac.Date).Days : 0; // 0 = في إجازة الآن
            }

            // 1ب) إجازات معتمدة لم تبدأ بعد
            result.UpcomingApproved = (await _service.GetUpcomingApprovedAsync(vacation.UserId, today)).Select(Item).ToList();

            // 2) الشهر الجاري
            (result.MonthApprovedCount, result.MonthApprovedDays) =
                await _service.GetApprovedInMonthAsync(vacation.UserId, today.Year, today.Month);

            // 3) الدفع لو اعتُمد الآن: نفس حساب الاعتماد النهائي (أيام العمل + الحد الشهري من المعتمد فقط)
            var type = await _types.GetByIdAsync(vacation.VacationTypeId);
            result.TypeIsPaid = type?.IsPaid ?? false;
            var holidays = await _holidays.GetDatesAsync(start, end);
            var usedPaid = await _service.GetApprovedPaidDaysByMonthAsync(vacation.UserId, start, end, vacation.Id);
            result.MonthlyQuota = MonthsBetween(start, end)
                .Select(m => new VacationMonthQuotaDto { Year = m.Year, Month = m.Month, PaidUsed = usedPaid.GetValueOrDefault((m.Year, m.Month)) })
                .ToList();
            var segments = VacationCalendar.Allocate(start, end, result.TypeIsPaid, holidays, new Dictionary<(int, int), int>(usedPaid));
            result.ProjectedPaidDays = segments.Where(s => s.IsPaid).Sum(s => s.Days);
            result.ProjectedUnpaidDays = segments.Where(s => !s.IsPaid).Sum(s => s.Days);
            result.ProjectedWorkingDays = result.ProjectedPaidDays + result.ProjectedUnpaidDays;

            // 4) زملاء القسم في إجازة تتداخل مع فترة الطلب (معتمدة أو معلّقة)
            result.DepartmentName = vacation.Department?.Name ?? string.Empty;
            result.DepartmentActiveUsers = (await _userService.GetByDepartmentAsync(vacation.DepartmentId)).Count(u => u.IsActive);
            var colleagues = await _service.GetOverlappingInDepartmentAsync(vacation.DepartmentId, vacation.UserId, start, end);
            result.ColleaguesOnLeave = colleagues.Select(Item).ToList();
            result.ColleaguesOnLeaveCount = colleagues.Select(v => v.UserId).Distinct().Count();

            // 6) طلبات أخرى معلّقة لنفس الموظف
            result.OtherPending = (await _service.GetOtherPendingAsync(vacation.UserId, vacation.Id)).Select(Item).ToList();

            return result;
        }

        private static VacationContextItemDto Item(Vacation v) => new()
        {
            Id = v.Id,
            UserName = v.User?.FullName ?? string.Empty,
            VacationTypeName = v.VacationType?.Name ?? string.Empty,
            StartVac = v.StartVac,
            EndVac = v.EndVac,
            VacDayCount = v.VacDayCount,
            Status = v.Status.ToString(),
            StatusAr = VacationRules.StatusAr(v.Status)
        };

        private static IEnumerable<DateTime> MonthsBetween(DateTime start, DateTime end)
        {
            for (var m = new DateTime(start.Year, start.Month, 1); m <= end; m = m.AddMonths(1))
                yield return m;
        }
    }
}
