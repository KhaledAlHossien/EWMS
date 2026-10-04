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
    /// </summary>
    public class ApproveVacationCommandHandler
      : IRequestHandler<ApproveVacationCommand, Unit>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notificationService;

        public ApproveVacationCommandHandler(
            IVacationService service,
            IUserService userService,
            IUserPermissionService permissions,
            INotificationService notificationService)
        {
            _service = service;
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
                    vacation.Status = VacationStatus.PendingBranchManager;
                }
                else MarkAsRejected(vacation, request.Dto.Reason);
            }
            else
            {
                vacation.BranchManagerAccept = request.Dto.Approve;
                if (request.Dto.Approve) vacation.Status = VacationStatus.Approved;
                else MarkAsRejected(vacation, request.Dto.Reason);
            }

            vacation.UpdatedAt = DateTime.UtcNow;
            await _service.UpdateAsync(vacation);

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

        private void MarkAsRejected(Domain.Entities.Vacation vacation, string? reason)
        {
            vacation.Status = VacationStatus.Rejected;
            vacation.RejectedByUserId = _userService.UserId;
            vacation.RejectionReason = string.IsNullOrWhiteSpace(reason)
                ? "بدون سبب محدد"
                : reason;
            vacation.RejectedAt = DateTime.UtcNow;
        }
    }
}
