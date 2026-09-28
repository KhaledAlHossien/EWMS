using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceSites.Commands.Update
{
    public record UpdateDeviceSiteCommand(int Id, UpdateDeviceSiteRequestDto DeviceSiteDto)
        : IRequest<DeviceSiteResponseDto>;
}
