using MediatR;

namespace Application.Features.Notifications.Commands.MarkAsRead
{
    public record MarkNotificationAsReadCommand(int Id) : IRequest<Unit>;
}
