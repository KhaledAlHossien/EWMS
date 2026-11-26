using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Commands.Update
{
    public record UpdateInstallationCommand(int Id, InstallationRequestDto Dto) : IRequest<DeviceSiteResponseDto>;
}
