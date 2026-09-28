using MediatR;

namespace Application.Features.DeviceSites.Commands.Delete
{
    public record DeleteDeviceSiteCommand(int Id) : IRequest<bool>;
}
