using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Notifications.Queries.GetMy
{
    public record GetMyNotificationsQuery(bool UnreadOnly = false) : IRequest<List<NotificationResponseDto>>;
}
