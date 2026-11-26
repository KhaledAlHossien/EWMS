using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.RequestStatuses.Queries.GetById
{
    public record GetMaintenanceRequestStatusByIdQuery(int Id) : IRequest<MaintenanceRequestStatusResponseDto>;
}
