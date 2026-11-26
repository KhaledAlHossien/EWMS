using MediatR;

namespace Application.Features.Maintenance.Tasks.Commands.Delete
{
    public record DeleteMaintenanceTaskCommand(int Id) : IRequest<Unit>;
}
