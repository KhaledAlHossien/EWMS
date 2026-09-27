using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace API.Hubs
{
    public class SignalRNotificationPusher : INotificationPusher
    {
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IMapper _mapper;
        private readonly ILogger<SignalRNotificationPusher> _logger;

        public SignalRNotificationPusher(
            IHubContext<NotificationHub> hub,
            IMapper mapper,
            ILogger<SignalRNotificationPusher> logger)
        {
            _hub = hub;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task PushAsync(IReadOnlyCollection<Notification> notifications)
        {
            foreach (var notification in notifications)
            {
                try
                {
                    var dto = _mapper.Map<NotificationResponseDto>(notification);
                    await _hub.Clients
                        .User(notification.UserId.ToString())
                        .SendAsync(NotificationHub.ReceiveMethod, dto);
                }
                catch (Exception ex)
                {
                    // الإشعار محفوظ في قاعدة البيانات أصلاً — المستخدم سيراه عند الاستعلام التالي
                    _logger.LogWarning(ex, "تعذر إرسال الإشعار {NotificationId} لحظياً", notification.Id);
                }
            }
        }
    }
}
