using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Devices.Commands.Create
{
    public record CreateDeviceCommand(CreateDeviceRequestDto DeviceDto)
        : IRequest<DeviceResponseDto>;
}
