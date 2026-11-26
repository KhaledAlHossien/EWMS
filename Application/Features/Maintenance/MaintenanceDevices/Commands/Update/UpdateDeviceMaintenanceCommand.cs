using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.MaintenanceDevices.Commands.Update
{
    public record UpdateDeviceMaintenanceCommand(int Id, DeviceMaintenanceRequestDto Dto) : IRequest<DeviceMaintenanceResponseDto>;
}
