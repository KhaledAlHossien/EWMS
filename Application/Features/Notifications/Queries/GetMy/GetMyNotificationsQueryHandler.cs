using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Notifications.Queries.GetMy
{
    public class GetMyNotificationsQueryHandler
        : IRequestHandler<GetMyNotificationsQuery, List<NotificationResponseDto>>
    {
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetMyNotificationsQueryHandler(
            INotificationService notificationService,
            IUserService userService,
            IMapper mapper)
        {
            _notificationService = notificationService;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<List<NotificationResponseDto>> Handle(
            GetMyNotificationsQuery request, CancellationToken ct)
        {
            var list = await _notificationService.GetByUserIdAsync(_userService.UserId, request.UnreadOnly);
            return _mapper.Map<List<NotificationResponseDto>>(list);
        }
    }
}
