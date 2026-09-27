using Application.Interfaces;
using MediatR;

namespace Application.Features.Notifications.Queries.GetUnreadCount
{
    public class GetUnreadNotificationCountQueryHandler
        : IRequestHandler<GetUnreadNotificationCountQuery, int>
    {
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;

        public GetUnreadNotificationCountQueryHandler(
            INotificationService notificationService,
            IUserService userService)
        {
            _notificationService = notificationService;
            _userService = userService;
        }

        public async Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct)
        {
            return await _notificationService.GetUnreadCountAsync(_userService.UserId);
        }
    }
}
