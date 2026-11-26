using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.DeviceTypes.Queries.GetAll
{
    public record GetAllDeviceTypesQuery : IRequest<List<DeviceTypeResponseDto>>;
}
