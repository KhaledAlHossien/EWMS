using Application.Features.Vacations;
using Application.Interfaces;
using Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Commands.Cancel
{
    public class CancelVacationCommandHandler : IRequestHandler<CancelVacationCommand, Unit>
    {
        private readonly IVacationService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notificationService;

        public CancelVacationCommandHandler(
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

        public async Task<Unit> Handle(CancelVacationCommand request, CancellationToken ct)
        {
            var vacation = await _service.GetByIdAsync(request.VacationId)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            // فقط صاحب الإجازة يستطيع الإلغاء
            if (vacation.UserId != _userService.UserId)
                throw new UnauthorizedAccessException("لا تستطيع إلغاء إجازة غيرك");

            // يُسمح بالإلغاء فقط قبل اعتماد رئيس الفرع الإداري نهائياً
            // (أي أثناء PendingManager أو PendingBranchManager)
            if (vacation.Status == VacationStatus.Approved)
                throw new InvalidOperationException("لا يمكن إلغاء إجازة معتمدة نهائياً");

            if (vacation.Status == VacationStatus.Rejected)
                throw new InvalidOperationException("الإجازة مرفوضة مسبقاً");

            if (vacation.Status == VacationStatus.Cancelled)
                throw new InvalidOperationException("الإجازة ملغاة مسبقاً");

            var hadReachedBranchManager = vacation.Status == VacationStatus.PendingBranchManager;

            // إلغاء ناعم (Soft Cancel): نحتفظ بالسجل لأغراض التدقيق بدل الحذف النهائي
            vacation.Status = VacationStatus.Cancelled;
            vacation.UpdatedAt = DateTime.UtcNow;

            await _service.UpdateAsync(vacation);

            // إشعار كل من كان يملك رؤية على هذا الطلب (رئيس القسم دائماً، ورئيس الفرع إن كان الطلب قد وصله)
            var employee = await _userService.GetByIdAsync(vacation.UserId);
            await VacationNotifier.NotifyCancelledAsync(
                _permissions, _notificationService, vacation, employee?.FullName ?? "موظف", hadReachedBranchManager);

            return Unit.Value;
        }
    }
}
