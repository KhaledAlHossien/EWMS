using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Notifications.Queries.GetPage
{
    /// <summary>صفحة من إشعاراتي (الأحدث أولاً) مع العدد الكلي — لصفحة الإشعارات؛ الجرس يبقى على GetMy</summary>
    public record GetMyNotificationsPageQuery(bool UnreadOnly, int Page, int PageSize) : IRequest<PagedResultDto<NotificationResponseDto>>;
}
