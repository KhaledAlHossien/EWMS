using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetById
{
    public record GetMaintenanceRequestByIdQuery(int Id) : IRequest<MaintenanceRequestResponseDto>;
}
