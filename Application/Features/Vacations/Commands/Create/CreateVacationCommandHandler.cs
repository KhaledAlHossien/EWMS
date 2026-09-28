using Application.DTOs.Response;
using Application.Features.Users;
using Application.Features.Vacations;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Create
{
    public class CreateVacationCommandHandler
       : IRequestHandler<CreateVacationCommand, List<VacationResponseDto>>
    {
        private readonly IVacationService _vacationService;
        private readonly IVacationTypeService _vacationTypeService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        // ==================== الإعدادات ====================
        private const int MaxPaidVacationDaysPerMonth = VacationRules.MaxPaidVacationDaysPerMonth;

        public CreateVacationCommandHandler(
            IVacationService vacationService,
            IVacationTypeService vacationTypeService,
            IUserService userService,
            INotificationService notificationService,
            IMapper mapper)
        {
            _vacationService = vacationService;
            _vacationTypeService = vacationTypeService;
            _userService = userService;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        public async Task<List<VacationResponseDto>> Handle(
            CreateVacationCommand request, CancellationToken ct)
        {
            var dto = request.VacationDto;

            // 1) تحقق من نوع الإجازة
            var vacationType = await _vacationTypeService.GetByIdAsync(dto.VacationTypeId)
                ?? throw new KeyNotFoundException("نوع الإجازة غير موجود");

            // 2) تحقق من التواريخ
            if (dto.EndVac < dto.StartVac)
                throw new ArgumentException("تاريخ النهاية يجب أن يكون بعد تاريخ البداية");

            // 3) تحقق من المستخدم
            var user = await _userService.GetByIdAsync(request.UserId)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            // رئيس الفرع و SuperAdmin لا يتبعان لقسم → لا يقدّمان إجازات من النظام (قرار المستخدم 2026-09-27)
            var roleName = user.Role?.Name ?? "";
            if (!UserPlacement.For(roleName).NeedsDepartment)
                throw new InvalidOperationException("لا يمكن لرئيس الفرع أو مدير النظام تقديم طلب إجازة من النظام");
            if (user.DepartmentId is not int departmentId || user.BranchId is not int branchId)
                throw new InvalidOperationException("حسابك غير مرتبط بقسم وفرع، تواصل مع المسؤول");

            // إجازة رئيس القسم تتجاوز مرحلة رئيس القسم وتذهب مباشرة لرئيس الفرع
            var initialStatus = roleName == "Manager"
                ? VacationStatus.PendingBranchManager
                : VacationStatus.PendingManager;

            // 4) تحقق من عدم وجود إجازة متداخلة (على كامل المدة المطلوبة)
            if (await _vacationService.HasOverlappingVacationAsync(
                    request.UserId, dto.StartVac, dto.EndVac))
                throw new InvalidOperationException("يوجد إجازة متداخلة في نفس الفترة");

            // 5) ✅ تقسيم المدة المطلوبة إلى مقاطع مدفوعة/غير مدفوعة حسب الحد الشهري
            //    مثال: استخدم الموظف يوماً مدفوعاً هذا الشهر وطلب 3 أيام إضافية
            //    → يوم واحد مدفوع (ضمن الحد) + يومان غير مدفوعين، كإجازتين منفصلتين
            var segments = await BuildPaidUnpaidSegmentsAsync(
                request.UserId, vacationType.IsPaid, dto.StartVac, dto.EndVac);

            // 6) إنشاء كيان لكل مقطع
            var created = new List<Vacation>();
            foreach (var segment in segments)
            {
                var vacation = new Vacation
                {
                    VacationTypeId = dto.VacationTypeId,
                    UserId = request.UserId,
                    DepartmentId = departmentId,
                    BranchId = branchId,
                    VacReason = dto.VacReason,
                    StartVac = segment.Start,
                    EndVac = segment.End,
                    VacDayCount = (segment.End - segment.Start).Days + 1,
                    Status = initialStatus,
                    IsPaid = segment.IsPaid,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _vacationService.AddAsync(vacation);
                created.Add(vacation);

                // إشعار من ينتظر قراره (رئيس القسم، أو رئيس الفرع لإجازة رئيس القسم) — لكل مقطع
                await VacationNotifier.NotifySubmittedAsync(
                    _userService, _notificationService, vacation, user.FullName);
            }

            // 7) إعادة القراءة مع العلاقات لكل إجازة تم إنشاؤها
            var result = new List<VacationResponseDto>();
            foreach (var vacation in created)
            {
                var withDetails = await _vacationService.GetWithDetailsAsync(vacation.Id) ?? vacation;
                result.Add(_mapper.Map<VacationResponseDto>(withDetails));
            }

            return result;
        }

        // ==================== المنطق الذكي ====================

        /// <summary>
        /// يقسّم المدة المطلوبة يوماً بيوم حسب الحد الشهري للأيام المدفوعة (2 يوم/شهر)،
        /// ثم يجمع الأيام المتتالية التي لها نفس الحالة (مدفوعة/غير مدفوعة) في مقطع واحد.
        /// - إذا كان نوع الإجازة غير مدفوع أصلاً → مقطع واحد غير مدفوع لكامل المدة.
        /// - إذا كانت كل الأيام ضمن الحد المتبقي → مقطع واحد مدفوع لكامل المدة.
        /// - إذا تجاوزت المدة الحد في شهر ما → يُقسَّم الطلب إلى أكثر من مقطع/إجازة.
        /// </summary>
        private async Task<List<(DateTime Start, DateTime End, bool IsPaid)>> BuildPaidUnpaidSegmentsAsync(
            int userId,
            bool vacationTypeIsPaid,
            DateTime startVac,
            DateTime endVac)
        {
            if (!vacationTypeIsPaid)
                return new List<(DateTime, DateTime, bool)> { (startVac, endVac, false) };

            // عدد الأيام المدفوعة المستخدمة مسبقاً في كل شهر يمر به الطلب (تُحمَّل عند الحاجة)
            var usedPaidDaysPerMonth = new Dictionary<(int Year, int Month), int>();

            var dailyStatus = new List<(DateTime Date, bool IsPaid)>();

            for (var date = startVac.Date; date <= endVac.Date; date = date.AddDays(1))
            {
                var monthKey = (date.Year, date.Month);

                if (!usedPaidDaysPerMonth.TryGetValue(monthKey, out var usedDays))
                {
                    usedDays = await _vacationService.GetPaidVacationDaysInMonthAsync(
                        userId, date.Year, date.Month);
                    usedPaidDaysPerMonth[monthKey] = usedDays;
                }

                var isPaidDay = usedDays < MaxPaidVacationDaysPerMonth;
                if (isPaidDay)
                    usedPaidDaysPerMonth[monthKey] = usedDays + 1;

                dailyStatus.Add((date, isPaidDay));
            }

            // تجميع الأيام المتتالية المتشابهة الحالة في مقاطع
            var segments = new List<(DateTime Start, DateTime End, bool IsPaid)>();
            var segStart = dailyStatus[0].Date;
            var segIsPaid = dailyStatus[0].IsPaid;

            for (var i = 1; i < dailyStatus.Count; i++)
            {
                if (dailyStatus[i].IsPaid != segIsPaid)
                {
                    segments.Add((segStart, dailyStatus[i - 1].Date, segIsPaid));
                    segStart = dailyStatus[i].Date;
                    segIsPaid = dailyStatus[i].IsPaid;
                }
            }

            segments.Add((segStart, dailyStatus[^1].Date, segIsPaid));
            return segments;
        }
    }
}
