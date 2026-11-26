using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Commands.Create
{
    public record CreateDeviceMaintenanceCommand(DeviceMaintenanceRequestDto Dto) : IRequest<DeviceMaintenanceResponseDto>;
}
