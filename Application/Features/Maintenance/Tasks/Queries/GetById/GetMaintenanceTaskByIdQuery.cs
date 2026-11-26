using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Queries.GetById
{
    public record GetMaintenanceTaskByIdQuery(int Id) : IRequest<MaintenanceTaskResponseDto>;
}
