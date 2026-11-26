using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Commands.Update
{
    public record UpdateDeviceTypeCommand(int Id, DeviceTypeRequestDto Dto) : IRequest<DeviceTypeResponseDto>;
}
