using MediatR;

namespace Application.Features.Devices.Commands.Delete
{
    public record DeleteDeviceCommand(int Id) : IRequest<bool>;
}
