using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Commands.Update
{
    public record UpdateMaintenanceRequestStatusCommand(int Id, MaintenanceRequestStatusRequestDto Dto) : IRequest<MaintenanceRequestStatusResponseDto>;
}
