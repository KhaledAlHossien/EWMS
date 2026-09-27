using MediatR;

namespace Application.Features.Notifications.Commands.MarkAllAsRead
{
    public record MarkAllNotificationsAsReadCommand : IRequest<Unit>;
}
