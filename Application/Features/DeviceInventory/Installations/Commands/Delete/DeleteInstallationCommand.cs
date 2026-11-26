using MediatR;

namespace Application.Features.DeviceInventory.Installations.Commands.Delete
{
    public record DeleteInstallationCommand(int Id) : IRequest<Unit>;
}
