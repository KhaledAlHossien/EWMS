using MediatR;

namespace Application.Features.Maintenance.DeviceCompanies.Commands.Delete
{
    public record DeleteDeviceCompanyCommand(int Id) : IRequest<Unit>;
}
