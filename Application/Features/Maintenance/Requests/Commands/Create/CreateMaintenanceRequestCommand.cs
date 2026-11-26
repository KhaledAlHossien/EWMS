using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.Create
{
    public record CreateMaintenanceRequestCommand(SaveMaintenanceRequestDto Dto) : IRequest<MaintenanceRequestResponseDto>;
}
