using MediatR;

namespace Application.Features.Notifications.Queries.GetUnreadCount
{
    public record GetUnreadNotificationCountQuery : IRequest<int>;
}
