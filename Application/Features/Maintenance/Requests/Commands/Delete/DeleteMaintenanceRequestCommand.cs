using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.Delete
{
    public record DeleteMaintenanceRequestCommand(int Id) : IRequest<Unit>;
}
