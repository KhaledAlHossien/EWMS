using Application.Interfaces;
using Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Approve
{
    public class ApproveVacationCommandHandler
      : IRequestHandler<ApproveVacationCommand, Unit>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;

        public ApproveVacationCommandHandler(
            IVacationService service,
            IUserService userService)
        {
            _service = service;
            _userService = userService;
        }

        public async Task<Unit> Handle(ApproveVacationCommand request, CancellationToken ct)
        {
            var vacation = await _service.GetByIdAsync(request.VacationId)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            // ══════════════════════════════════════════════════════
            // 1. من أنا؟
            // ══════════════════════════════════════════════════════
            var currentUser = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

            var roleName = currentUser.Role?.Name ?? "";
            var isSuperAdmin = roleName == "SuperAdmin";

            // ══════════════════════════════════════════════════════
            // 2. فحوصات الحالة
            // ══════════════════════════════════════════════════════
            if (vacation.Status == VacationStatus.Approved)
                throw new InvalidOperationException("الإجازة معتمدة نهائياً");

            if (vacation.Status == VacationStatus.Rejected)
                throw new InvalidOperationException("الإجازة مرفوضة مسبقاً");

            // ══════════════════════════════════════════════════════
            // 3. switch حسب المرحلة + التحقق من الدور
            // ══════════════════════════════════════════════════════
            switch (vacation.Status)
            {
                case VacationStatus.PendingManager:

                    // هل أنا Manager AND في نفس القسم؟ (أو SuperAdmin)
                    if (!isSuperAdmin &&
                        (roleName != "Manager" ||
                         currentUser.DepartmentId != vacation.DepartmentId))
                        throw new UnauthorizedAccessException(
                            "لا تملك صلاحية الموافقة على إجازات خارج قسمك");

                    vacation.ManagerAccept = request.Dto.Approve;

                    if (request.Dto.Approve)
                        vacation.Status = VacationStatus.PendingBranchManager;
                    else
                        MarkAsRejected(vacation, request.Dto.Reason);
                    break;

                case VacationStatus.PendingBranchManager:

                    // هل أنا BranchManager AND في نفس الفرع؟ (أو SuperAdmin)
                    if (!isSuperAdmin &&
                        (roleName != "BranchManager" ||
                         currentUser.BranchId != vacation.BranchId))
                        throw new UnauthorizedAccessException(
                            "لا تملك صلاحية الموافقة على إجازات خارج فرعك");

                    vacation.BranchManagerAccept = request.Dto.Approve;

                    if (request.Dto.Approve)
                        vacation.Status = VacationStatus.Approved;
                    else
                        MarkAsRejected(vacation, request.Dto.Reason);
                    break;

                default:
                    throw new InvalidOperationException("حالة الإجازة غير معروفة");
            }

            vacation.UpdatedAt = DateTime.UtcNow;
            await _service.UpdateAsync(vacation);
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
