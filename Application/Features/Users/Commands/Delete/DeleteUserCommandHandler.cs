using Application.Interfaces;
using MediatR;

namespace Application.Features.Users.Commands.Delete
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;

        public DeleteUserCommandHandler(IUserService userService, ICurrentUserService currentUserService)
        {
            _userService = userService;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            if (user.Id == _currentUserService.UserId)
                throw new InvalidOperationException("لا يمكنك حذف حسابك الحالي");

            UserRules.EnsureCanChangeExistingUser(_currentUserService, user);

            if (await _userService.IsUserUsedAsync(user.Id))
                throw new InvalidOperationException("لا يمكن حذف مستخدم مرتبط بمشاريع أو ملفات أو تحويلات");

            return await _userService.DeleteAsync(user);
        }
    }
}
