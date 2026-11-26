using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Queries.GetById
{
    public record GetDeviceTypeByIdQuery(int Id) : IRequest<DeviceTypeResponseDto>;
}
