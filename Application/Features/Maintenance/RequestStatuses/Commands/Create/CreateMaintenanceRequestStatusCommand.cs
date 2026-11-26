using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Commands.Create
{
    public record CreateMaintenanceRequestStatusCommand(MaintenanceRequestStatusRequestDto Dto) : IRequest<MaintenanceRequestStatusResponseDto>;
}
