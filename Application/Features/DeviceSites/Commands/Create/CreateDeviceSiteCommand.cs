using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceSites.Commands.Create
{
    public record CreateDeviceSiteCommand(CreateDeviceSiteRequestDto DeviceSiteDto)
        : IRequest<DeviceSiteResponseDto>;
}
