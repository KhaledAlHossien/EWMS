using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Commands.Create
{
    public record CreateMaintenanceTaskCommand(SaveMaintenanceTaskDto Dto) : IRequest<MaintenanceTaskResponseDto>;
}
