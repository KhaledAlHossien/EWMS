using Application.Interfaces;
using MediatR;

namespace Application.Features.Offices.Commands.Delete
{
    public class DeleteOfficeCommandHandler : IRequestHandler<DeleteOfficeCommand, bool>
    {
        private readonly IOfficeService _officeService;
        private readonly ICurrentUserService _currentUserService;

        public DeleteOfficeCommandHandler(
            IOfficeService officeService,
            ICurrentUserService currentUserService)
        {
            _officeService = officeService;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(DeleteOfficeCommand request, CancellationToken cancellationToken)
        {
            var office = await _officeService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المكتب غير موجود");

            if (!_currentUserService.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                && _currentUserService.DepartmentId != office.DepartmentId)
                throw new UnauthorizedAccessException("لا يمكنك حذف مكتب خارج قسمك");

            // منع الحذف لو فيه موظفون منتمون لهذا المكتب
            if (await _officeService.HasUsersAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف مكتب يحتوي على موظفين");

            return await _officeService.DeleteAsync(office);
        }
    }
}
