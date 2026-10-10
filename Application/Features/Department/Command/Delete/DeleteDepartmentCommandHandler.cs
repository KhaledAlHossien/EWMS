using Application.Common;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Department.Command.Delete
{
    public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand, bool>
    {
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAssignedTaskService _assignedTaskService;
        private readonly IMaintenanceRequestService _maintenanceRequestService;
        private readonly IMaintenanceTaskService _maintenanceTaskService;
        private readonly ISparePartService _sparePartService;

        public DeleteDepartmentCommandHandler(
            IDepartmentService departmentService,
            ICurrentUserService currentUserService,
            IAssignedTaskService assignedTaskService,
            IMaintenanceRequestService maintenanceRequestService,
            IMaintenanceTaskService maintenanceTaskService,
            ISparePartService sparePartService)
        {
            _departmentService = departmentService;
            _currentUserService = currentUserService;
            _assignedTaskService = assignedTaskService;
            _maintenanceRequestService = maintenanceRequestService;
            _maintenanceTaskService = maintenanceTaskService;
            _sparePartService = sparePartService;
        }

        public async Task<bool> Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
        {
            var department = await _departmentService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            var me = await _currentUserService.GetUserAsync();
            if (!OrganizationRole.IsSystemAdmin(me) && !OrganizationRole.InBranch(me, department.BranchId))
                throw new UnauthorizedAccessException("لا يمكنك حذف قسم خارج فرعك");

            // منع الحذف لو فيه مستخدمون
            if (await _departmentService.HasUsersAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف قسم يحتوي على مستخدمين");

            // منع الحذف لو فيه مكاتب
            if (await _departmentService.HasOfficesAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف قسم يحتوي على مكاتب");

            // سجل المهام المُسندة للقسم محفوظ (Restrict)
            if (await _assignedTaskService.ExistsForDepartmentAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف قسم له مهام على لوحة المهام");

            if (await _maintenanceRequestService.ExistsForDepartmentAsync(request.Id)
                || await _maintenanceTaskService.ExistsForDepartmentAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف قسم له طلبات أو مهام صيانة مسجّلة");

            if (await _sparePartService.ExistsForDepartmentAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف قسم له مخزون قطع غيار");

            return await _departmentService.DeleteAsync(department);
        }
    }
}
