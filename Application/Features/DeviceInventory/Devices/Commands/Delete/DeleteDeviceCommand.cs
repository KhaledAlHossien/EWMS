using MediatR;

namespace Application.Features.DeviceInventory.Devices.Commands.Delete
{
    public record DeleteDeviceCommand(int Id) : IRequest<Unit>;
}
