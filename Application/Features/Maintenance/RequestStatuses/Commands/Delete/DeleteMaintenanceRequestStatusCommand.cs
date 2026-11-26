using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Commands.Delete
{
    public record DeleteMaintenanceRequestStatusCommand(int Id) : IRequest<Unit>;
}
