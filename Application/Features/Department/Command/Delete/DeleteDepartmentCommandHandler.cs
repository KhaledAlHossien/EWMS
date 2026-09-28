using Application.Interfaces;
using MediatR;

namespace Application.Features.Department.Command.Delete
{
    public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand, bool>
    {
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAssignedTaskService _assignedTaskService;

        public DeleteDepartmentCommandHandler(
            IDepartmentService departmentService,
            ICurrentUserService currentUserService,
            IAssignedTaskService assignedTaskService)
        {
            _departmentService = departmentService;
            _currentUserService = currentUserService;
            _assignedTaskService = assignedTaskService;
        }

        public async Task<bool> Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
        {
            var department = await _departmentService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("القسم غير موجود");

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && _currentUserService.BranchId != department.BranchId)
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

            return await _departmentService.DeleteAsync(department);
        }
    }
}
