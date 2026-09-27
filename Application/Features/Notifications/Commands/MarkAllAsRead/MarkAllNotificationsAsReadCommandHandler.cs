using Application.Interfaces;
using MediatR;

namespace Application.Features.Notifications.Commands.MarkAllAsRead
{
    public class MarkAllNotificationsAsReadCommandHandler
        : IRequestHandler<MarkAllNotificationsAsReadCommand, Unit>
    {
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;

        public MarkAllNotificationsAsReadCommandHandler(
            INotificationService notificationService,
            IUserService userService)
        {
            _notificationService = notificationService;
            _userService = userService;
        }

        public async Task<Unit> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken ct)
        {
            await _notificationService.MarkAllAsReadAsync(_userService.UserId);
            return Unit.Value;
        }
    }
}
