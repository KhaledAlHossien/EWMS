using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Devices.Queries.GetAll
{
    public record GetAllDevicesQuery : IRequest<List<DeviceResponseDto>>;
}
