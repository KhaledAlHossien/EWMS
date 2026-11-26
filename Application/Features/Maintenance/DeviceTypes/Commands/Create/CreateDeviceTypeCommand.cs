using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Commands.Create
{
    public record CreateDeviceTypeCommand(DeviceTypeRequestDto Dto) : IRequest<DeviceTypeResponseDto>;
}
