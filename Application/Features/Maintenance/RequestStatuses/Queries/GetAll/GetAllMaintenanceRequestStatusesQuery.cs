using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Queries.GetAll
{
    public record GetAllMaintenanceRequestStatusesQuery : IRequest<List<MaintenanceRequestStatusResponseDto>>;
}
