using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Commands.Update
{
    public record UpdateMaintenanceTaskCommand(int Id, SaveMaintenanceTaskDto Dto) : IRequest<MaintenanceTaskResponseDto>;
}
