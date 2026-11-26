using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Commands.Delete
{
    public record DeleteDeviceMaintenanceCommand(int Id) : IRequest<Unit>;
}
