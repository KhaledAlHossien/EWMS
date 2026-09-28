using Application.Interfaces;
using MediatR;

namespace Application.Features.Offices.Commands.Delete
{
    public class DeleteOfficeCommandHandler : IRequestHandler<DeleteOfficeCommand, bool>
    {
        private readonly IOfficeService _officeService;
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAssignedTaskService _assignedTaskService;

        public DeleteOfficeCommandHandler(
            IOfficeService officeService,
            IDepartmentService departmentService,
            ICurrentUserService currentUserService,
            IAssignedTaskService assignedTaskService)
        {
            _officeService = officeService;
            _departmentService = departmentService;
            _currentUserService = currentUserService;
            _assignedTaskService = assignedTaskService;
        }

        public async Task<bool> Handle(DeleteOfficeCommand request, CancellationToken cancellationToken)
        {
            var office = await _officeService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            await OfficeRules.EnsureCanManageDepartmentAsync(
                _currentUserService, _departmentService, office.DepartmentId,
                "لا يمكنك حذف مكتب خارج نطاقك");

            // منع الحذف لو فيه موظفون منتمون لهذا المكتب
            if (await _officeService.HasUsersAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف مكتب يحتوي على موظفين");

            if (await _assignedTaskService.ExistsForOfficeAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف مكتب له مهام على لوحة المهام");

            return await _officeService.DeleteAsync(office);
        }
    }
}
