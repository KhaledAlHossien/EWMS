using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Queries.GetById
{
    public record GetDeviceMaintenanceByIdQuery(int Id) : IRequest<DeviceMaintenanceResponseDto>;
}
