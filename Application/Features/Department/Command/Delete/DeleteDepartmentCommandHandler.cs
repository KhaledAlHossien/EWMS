using Application.Interfaces;
using MediatR;

namespace Application.Features.Department.Command.Delete
{
    public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand, bool>
    {
        private readonly IDepartmentService _departmentService;

        public DeleteDepartmentCommandHandler(IDepartmentService departmentService)
        {
            _departmentService = departmentService;
        }

        public async Task<bool> Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
        {
            var department = await _departmentService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("القسم غير موجود");

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