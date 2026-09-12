using Application.Interfaces;
using MediatR;

namespace Application.Features.Department.Command.Delete
{
    public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand, bool>
    {
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;

        public DeleteDepartmentCommandHandler(
            IDepartmentService departmentService,
            ICurrentUserService currentUserService)
        {
            _departmentService = departmentService;
            _currentUserService = currentUserService;
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

            // منع الحذف لو فيه مشاريع
            if (await _departmentService.HasProjectsAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف قسم يحتوي على مشاريع");

            return await _departmentService.DeleteAsync(department);
        }
    }
}
