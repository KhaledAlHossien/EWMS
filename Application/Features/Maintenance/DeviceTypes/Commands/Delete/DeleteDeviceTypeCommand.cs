using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Commands.Delete
{
    public record DeleteDeviceTypeCommand(int Id) : IRequest<Unit>;
}
