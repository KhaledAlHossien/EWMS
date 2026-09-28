using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Devices.Commands.Update
{
    public record UpdateDeviceCommand(int Id, UpdateDeviceRequestDto DeviceDto)
        : IRequest<DeviceResponseDto>;
}
