using Application.DTOs.Response;
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
       : IRequestHandler<CreateVacationCommand, VacationResponseDto>
    {
        private readonly IVacationService _vacationService;
        private readonly IVacationTypeService _vacationTypeService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        // ==================== الإعدادات ====================
        private const int MaxPaidVacationDaysPerMonth = 2;

        public CreateVacationCommandHandler(
            IVacationService vacationService,
            IVacationTypeService vacationTypeService,
            IUserService userService,
            IMapper mapper)
        {
            _vacationService = vacationService;
            _vacationTypeService = vacationTypeService;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<VacationResponseDto> Handle(
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

            // 4) تحقق من عدم وجود إجازة متداخلة
            if (await _vacationService.HasOverlappingVacationAsync(
                    request.UserId, dto.StartVac, dto.EndVac))
                throw new InvalidOperationException("يوجد إجازة متداخلة في نفس الفترة");

            // 5) ✅ تحديد هل الإجازة مدفوعة أم لا
            var isPaid = await DetermineIsPaidAsync(
                request.UserId,
                vacationType,
                dto.StartVac,
                dto.EndVac);

            // 6) إنشاء الكيان
            var vacation = new Vacation
            {
                VacationTypeId = dto.VacationTypeId,
                UserId = request.UserId,
                DepartmentId = user.DepartmentId,
                BranchId = user.BranchId,
                VacReason = dto.VacReason,
                StartVac = dto.StartVac,
                EndVac = dto.EndVac,
                VacDayCount = (dto.EndVac - dto.StartVac).Days + 1,
                Status = VacationStatus.PendingManager,
                IsPaid = isPaid,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _vacationService.AddAsync(vacation);

            // 7) إعادة القراءة مع العلاقات
            var created = await _vacationService.GetWithDetailsAsync(vacation.Id);
            return _mapper.Map<VacationResponseDto>(created);
        }

        // ==================== المنطق الذكي ====================

        /// <summary>
        /// يحدد إذا كانت الإجازة مدفوعة أم لا:
        /// 1. إذا نوع الإجازة غير مدفوع → غير مدفوعة دائماً
        /// 2. إذا نوع الإجازة مدفوع → تحقق من الحد الشهري:
        ///    - إذا لم يتجاوز الحد → مدفوعة
        ///    - إذا تجاوز الحد → تصبح غير مدفوعة
        /// </summary>
        private async Task<bool> DetermineIsPaidAsync(
            int userId,
            VacationType vacationType,
            DateTime startVac,
            DateTime endVac)
        {
            // ───────── الحالة 1: النوع غير مدفوع ─────────
            if (!vacationType.IsPaid)
                return false;

            // ───────── الحالة 2: النوع مدفوع → تحقق من الحد ─────────
            var monthsInRange = GetMonthsInRange(startVac, endVac);

            foreach (var (year, month) in monthsInRange)
            {
                var usedPaidDays = await _vacationService
                    .GetPaidVacationDaysInMonthAsync(userId, year, month);

                var newDaysInMonth = GetDaysInMonthForRange(
                    startVac, endVac, year, month);

                if (usedPaidDays + newDaysInMonth > MaxPaidVacationDaysPerMonth)
                    return false;   // ❌ تجاوز الحد → غير مدفوعة
            }

            return true;   // ✅ مدفوعة
        }

        // ==================== دوال مساعدة ====================

        private static List<(int Year, int Month)> GetMonthsInRange(
            DateTime start, DateTime end)
        {
            var months = new List<(int Year, int Month)>();
            var cursor = new DateTime(start.Year, start.Month, 1);

            while (cursor <= end)
            {
                months.Add((cursor.Year, cursor.Month));
                cursor = cursor.AddMonths(1);
            }

            return months;
        }

        private static int GetDaysInMonthForRange(
            DateTime start, DateTime end, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var overlapStart = start > monthStart ? start : monthStart;
            var overlapEnd = end < monthEnd ? end : monthEnd;

            if (overlapStart > overlapEnd) return 0;

            return (overlapEnd - overlapStart).Days + 1;
        }
    }
}