using Application.Features.Vacations;
using Application.Interfaces;
using Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

using Application.Common;

namespace Application.Features.Vacations.Commands.Approve
{
    /// <summary>
    /// قرار على إجازة في مرحلتها الحالية (Role-Permission، قرار المستخدم 2026-10-03):
    /// - PendingManager: يحتاج ApproveVacationFirst والإجازة من فرعي.
    /// - PendingBranchManager: يحتاج ApproveVacationFinal والإجازة من فرعي، ولستُ من وافق عليها في المرحلة الأولى.
    /// - لا أحد يقرر في إجازته الخاصة. SuperAdmin يتجاوز كل ذلك.
    /// عند الاعتماد النهائي يُحدَّد الدفع (قرار المستخدم 2026-10-04): تُعاد أيام العمل بالعطل الحالية،
    /// وتُوزَّع على أجزاء مدفوعة/غير مدفوعة بحسب ما اعتُمد مسبقاً هذا الشهر، تحت قفل على الموظف.
    /// </summary>
    public class ApproveVacationCommandHandler
      : IRequestHandler<ApproveVacationCommand, Unit>
    {
        private readonly IVacationService _service;
        private readonly IVacationTypeService _vacationTypes;
        private readonly IPublicHolidayService _holidays;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notificationService;

        public ApproveVacationCommandHandler(
            IVacationService service,
            IVacationTypeService vacationTypes,
            IPublicHolidayService holidays,
            IUserService userService,
            IUserPermissionService permissions,
            INotificationService notificationService)
        {
            _service = service;
            _vacationTypes = vacationTypes;
            _holidays = holidays;
            _userService = userService;
            _permissions = permissions;
            _notificationService = notificationService;
        }

        public async Task<Unit> Handle(ApproveVacationCommand request, CancellationToken ct)
        {
            var vacation = await _service.GetByIdAsync(request.VacationId)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            var isSuperAdmin = viewer.IsSuperAdmin;

            // ══════════ 1. فحوصات الحالة ══════════
            if (vacation.Status == VacationStatus.Approved)
                throw new InvalidOperationException("الإجازة معتمدة نهائياً");
            if (vacation.Status == VacationStatus.Rejected)
                throw new InvalidOperationException("الإجازة مرفوضة مسبقاً");
            if (vacation.Status == VacationStatus.Cancelled)
                throw new InvalidOperationException("الإجازة ملغاة");

            var permission = VacationAccess.PermissionForStage(vacation.Status)
                ?? throw new InvalidOperationException("حالة الإجازة غير معروفة");

            // ══════════ 2. من يقرر في هذه المرحلة ══════════
            if (!isSuperAdmin)
            {
                if (vacation.UserId == viewer.Id)
                    throw new UnauthorizedAccessException("لا يمكنك الموافقة على إجازتك الخاصة");

                if (!viewer.Has(permission))
                    throw new UnauthorizedAccessException(vacation.Status == VacationStatus.PendingManager
                        ? "لا تملك صلاحية الموافقة الأولى على الإجازات"
                        : "لا تملك صلاحية الاعتماد النهائي للإجازات");

                if (viewer.User.BranchId != vacation.BranchId)
                    throw new UnauthorizedAccessException("لا تملك صلاحية الموافقة على إجازات خارج فرعك");

                if (vacation.Status == VacationStatus.PendingBranchManager && vacation.FirstApprovedByUserId == viewer.Id)
                    throw new UnauthorizedAccessException("وافقت على هذه الإجازة في المرحلة الأولى، والاعتماد النهائي لشخص آخر");
            }

            // ══════════ 3. القرار ══════════
            var stageBeforeDecision = vacation.Status;

            if (stageBeforeDecision == VacationStatus.PendingManager)
            {
                vacation.ManagerAccept = request.Dto.Approve;
                if (request.Dto.Approve)
                {
                    vacation.FirstApprovedByUserId = viewer.Id;
                    vacation.FirstApprovedAt = DateTime.UtcNow;
                    vacation.Status = VacationStatus.PendingBranchManager;
                }
                else MarkAsRejected(vacation, request.Dto.Reason);

                vacation.UpdatedAt = DateTime.UtcNow;
                await _service.UpdateAsync(vacation);
            }
            else if (request.Dto.Approve)
            {
                await _service.RunExclusiveForUserAsync(vacation.UserId, async () =>
                {
                    await AllocatePaymentAsync(vacation);
                    vacation.BranchManagerAccept = true;
                    vacation.FinalApprovedByUserId = viewer.Id;
                    vacation.FinalApprovedAt = DateTime.UtcNow;
                    vacation.Status = VacationStatus.Approved;
                    vacation.UpdatedAt = DateTime.UtcNow;
                    await _service.UpdateAsync(vacation);
                    return true;
                });
            }
            else
            {
                vacation.BranchManagerAccept = false;
                MarkAsRejected(vacation, request.Dto.Reason);
                vacation.UpdatedAt = DateTime.UtcNow;
                await _service.UpdateAsync(vacation);
            }

            // ══════════ 4. الإشعارات ══════════
            var employee = await _userService.GetByIdAsync(vacation.UserId);
            var employeeFullName = employee?.FullName ?? "موظف";

            if (stageBeforeDecision == VacationStatus.PendingManager)
            {
                if (request.Dto.Approve)
                    await VacationNotifier.NotifyApprovedByManagerAsync(
                        _permissions, _notificationService, vacation, employeeFullName);
                else
                    await VacationNotifier.NotifyRejectedByManagerAsync(
                        _notificationService, vacation, request.Dto.Reason);
            }
            else
            {
                if (request.Dto.Approve)
                    await VacationNotifier.NotifyApprovedFinalAsync(
                        _notificationService, vacation, employeeFullName);
                else
                    await VacationNotifier.NotifyRejectedByBranchManagerAsync(
                        _notificationService, vacation, employeeFullName, request.Dto.Reason);
            }

            return Unit.Value;
        }

        /// <summary>
        /// يوزّع أيام العمل على أجزاء مدفوعة/غير مدفوعة: الحد الشهري مشترك بين كل الأنواع المدفوعة،
        /// ويُخصم منه فقط ما اعتُمد نهائياً قبل هذه الإجازة. النوع غير المدفوع → كل الأيام غير مدفوعة.
        /// </summary>
        private async Task AllocatePaymentAsync(Domain.Entities.Vacation vacation)
        {
            var type = await _vacationTypes.GetByIdAsync(vacation.VacationTypeId)
                ?? throw new KeyNotFoundException("نوع الإجازة غير موجود");

            var holidays = await _holidays.GetDatesAsync(vacation.StartVac, vacation.EndVac);
            var usedPaid = await _service.GetApprovedPaidDaysByMonthAsync(
                vacation.UserId, vacation.StartVac, vacation.EndVac, vacation.Id);

            var segments = VacationCalendar.Allocate(
                vacation.StartVac, vacation.EndVac, type.IsPaid, holidays, usedPaid);

            vacation.Segments.Clear();
            foreach (var segment in segments) vacation.Segments.Add(segment);

            vacation.PaidDays = segments.Where(x => x.IsPaid).Sum(x => x.Days);
            vacation.UnpaidDays = segments.Where(x => !x.IsPaid).Sum(x => x.Days);
            vacation.VacDayCount = vacation.PaidDays + vacation.UnpaidDays; // بالعطل المسجّلة وقت الاعتماد
            vacation.IsPaid = vacation.PaidDays > 0;
        }

        private void MarkAsRejected(Domain.Entities.Vacation vacation, string? reason)
        {
            vacation.RejectedAtStage = vacation.Status;
            vacation.Status = VacationStatus.Rejected;
            vacation.RejectedByUserId = _userService.UserId;
            vacation.RejectionReason = string.IsNullOrWhiteSpace(reason)
                ? "بدون سبب محدد"
                : reason;
            vacation.RejectedAt = DateTime.UtcNow;
        }
    }
}
