using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Notifications.Queries.GetPage
{
    /// <summary>صفحة من إشعاراتي (الأحدث أولاً) مع العدد الكلي — لصفحة الإشعارات؛ الجرس يبقى على GetMy</summary>
    public record GetMyNotificationsPageQuery(bool UnreadOnly, int Page, int PageSize) : IRequest<PagedResultDto<NotificationResponseDto>>;

    public class GetMyNotificationsPageQueryHandler
        : IRequestHandler<GetMyNotificationsPageQuery, PagedResultDto<NotificationResponseDto>>
    {
        public const int MaxPageSize = 100;

        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetMyNotificationsPageQueryHandler(INotificationService notificationService, IUserService userService, IMapper mapper)
        {
            _notificationService = notificationService;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<PagedResultDto<NotificationResponseDto>> Handle(GetMyNotificationsPageQuery request, CancellationToken ct)
        {
            var page = Math.Max(1, request.Page);
            var size = Math.Clamp(request.PageSize, 1, MaxPageSize);
            var (items, total) = await _notificationService.GetPageByUserIdAsync(_userService.UserId, request.UnreadOnly, page, size);
            return new PagedResultDto<NotificationResponseDto>
            {
                Items = _mapper.Map<List<NotificationResponseDto>>(items),
                TotalCount = total, Page = page, PageSize = size
            };
        }
    }
}
