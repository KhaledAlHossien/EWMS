using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Commands.Create
{
    public record CreateInstallationCommand(InstallationRequestDto Dto) : IRequest<DeviceSiteResponseDto>;
}
