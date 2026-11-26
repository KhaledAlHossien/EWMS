using MediatR;

namespace Application.Features.DeviceInventory.Sites.Commands.Delete
{
    public record DeleteSiteCommand(int Id) : IRequest<Unit>;
}
