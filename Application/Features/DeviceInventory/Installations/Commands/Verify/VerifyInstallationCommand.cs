using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Commands.Verify
{
    public record VerifyInstallationCommand(int Id) : IRequest<DeviceSiteResponseDto>;
}
