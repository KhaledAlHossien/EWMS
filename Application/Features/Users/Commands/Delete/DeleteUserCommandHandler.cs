using Application.Interfaces;
using MediatR;

namespace Application.Features.Users.Commands.Delete
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAssignedTaskService _assignedTaskService;
        private readonly IMaintenanceRequestService _maintenanceRequestService;
        private readonly IMaintenanceTaskService _maintenanceTaskService;
        private readonly IVacationService _vacationService;
        private readonly ISparePartService _sparePartService;
        private readonly IDeviceInventoryLogService _deviceLog;

        public DeleteUserCommandHandler(
            IUserService userService,
            ICurrentUserService currentUserService,
            IAssignedTaskService assignedTaskService,
            IMaintenanceRequestService maintenanceRequestService,
            IMaintenanceTaskService maintenanceTaskService,
            IVacationService vacationService,
            ISparePartService sparePartService,
            IDeviceInventoryLogService deviceLog)
        {
            _sparePartService = sparePartService;
            _deviceLog = deviceLog;
            _userService = userService;
            _currentUserService = currentUserService;
            _assignedTaskService = assignedTaskService;
            _maintenanceRequestService = maintenanceRequestService;
            _maintenanceTaskService = maintenanceTaskService;
            _vacationService = vacationService;
        }

        public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            if (user.Id == _currentUserService.UserId)
                throw new InvalidOperationException("لا يمكنك حذف حسابك الحالي");

            await UserRules.EnsureCanChangeExistingUserAsync(_currentUserService, user);

            // أسند مهام أو أُسندت إليه أو شارك في سجلها — يُعطَّل الحساب بدل حذفه
            if (await _vacationService.ExistsForUserAsync(user.Id))
                throw new InvalidOperationException("لا يمكن حذف موظف له إجازات أو قرارات على إجازات — عطّل حسابه بدلاً من ذلك");

            if (await _assignedTaskService.ExistsForUserAsync(user.Id))
                throw new InvalidOperationException("لا يمكن حذف موظف مرتبط بمهام على لوحة المهام — عطّل حسابه بدلاً من ذلك");

            if (await _maintenanceRequestService.ExistsForUserAsync(user.Id)
                || await _maintenanceTaskService.ExistsForUserAsync(user.Id))
                throw new InvalidOperationException("لا يمكن حذف موظف له طلبات أو مهام صيانة مسجّلة — عطّل حسابه بدلاً من ذلك");

            if (await _deviceLog.ExistsForUserAsync(user.Id))
                throw new InvalidOperationException("لا يمكن حذف موظف له سجل في توثيق الأجهزة — عطّل حسابه بدلاً من ذلك");

            if (await _sparePartService.ExistsForUserAsync(user.Id))
                throw new InvalidOperationException("لا يمكن حذف موظف له حركات على مخزون قطع الغيار — عطّل حسابه بدلاً من ذلك");


            return await _userService.DeleteAsync(user);
        }
    }
}
