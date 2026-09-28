using Application.Interfaces;
using MediatR;

namespace Application.Features.Users.Commands.Delete
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAssignedTaskService _assignedTaskService;

        public DeleteUserCommandHandler(
            IUserService userService,
            ICurrentUserService currentUserService,
            IAssignedTaskService assignedTaskService)
        {
            _userService = userService;
            _currentUserService = currentUserService;
            _assignedTaskService = assignedTaskService;
        }

        public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            if (user.Id == _currentUserService.UserId)
                throw new InvalidOperationException("لا يمكنك حذف حسابك الحالي");

            UserRules.EnsureCanChangeExistingUser(_currentUserService, user);

            // أسند مهام أو أُسندت إليه أو شارك في سجلها — يُعطَّل الحساب بدل حذفه
            if (await _assignedTaskService.ExistsForUserAsync(user.Id))
                throw new InvalidOperationException("لا يمكن حذف موظف مرتبط بمهام على لوحة المهام — عطّل حسابه بدلاً من ذلك");

           

            return await _userService.DeleteAsync(user);
        }
    }
}
