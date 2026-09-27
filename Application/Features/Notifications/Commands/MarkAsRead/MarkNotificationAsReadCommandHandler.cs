using Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Features.Notifications.Commands.MarkAsRead
{
    public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, Unit>
    {
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;

        public MarkNotificationAsReadCommandHandler(
            INotificationService notificationService,
            IUserService userService)
        {
            _notificationService = notificationService;
            _userService = userService;
        }

        public async Task<Unit> Handle(MarkNotificationAsReadCommand request, CancellationToken ct)
        {
            var notification = await _notificationService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الإشعار غير موجود");

            if (notification.UserId != _userService.UserId)
                throw new UnauthorizedAccessException("لا يمكنك الوصول لإشعار غيرك");

            if (!notification.IsRead)
                await _notificationService.MarkAsReadAsync(notification);

            return Unit.Value;
        }
    }
}
