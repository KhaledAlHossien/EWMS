using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Devices.Commands.Create
{
    public record CreateDeviceCommand(DeviceRequestDto Dto) : IRequest<DeviceResponseDto>;
}
