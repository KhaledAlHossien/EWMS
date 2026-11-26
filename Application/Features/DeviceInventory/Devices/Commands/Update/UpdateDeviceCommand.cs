using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Devices.Commands.Update
{
    public record UpdateDeviceCommand(int Id, DeviceRequestDto Dto) : IRequest<DeviceResponseDto>;
}
