using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Commands.Update
{
    public record UpdateMaintenanceRequestCommand(int Id, SaveMaintenanceRequestDto Dto) : IRequest<MaintenanceRequestResponseDto>;
}
