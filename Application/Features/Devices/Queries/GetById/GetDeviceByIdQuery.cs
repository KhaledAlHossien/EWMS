using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Devices.Queries.GetById
{
    public record GetDeviceByIdQuery(int Id) : IRequest<DeviceResponseDto>;
}
