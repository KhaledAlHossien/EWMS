using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Queries.GetBySerial
{
    public record GetDeviceMaintenanceBySerialQuery(string SerialNumber) : IRequest<DeviceMaintenanceResponseDto>;
}
