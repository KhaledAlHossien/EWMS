using Application.Interfaces;
using MediatR;

namespace Application.Features.Offices.Commands.Delete
{
    public class DeleteOfficeCommandHandler : IRequestHandler<DeleteOfficeCommand, bool>
    {
        private readonly IOfficeService _officeService;
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;

        public DeleteOfficeCommandHandler(
            IOfficeService officeService,
            IDepartmentService departmentService,
            ICurrentUserService currentUserService)
        {
            _officeService = officeService;
            _departmentService = departmentService;
            _currentUserService = currentUserService;
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

            return await _officeService.DeleteAsync(office);
        }
    }
}
