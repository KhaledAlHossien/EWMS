using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetMovements
{
    public record GetSparePartMovementsQuery(int Id, int Page, int PageSize) : IRequest<PagedResultDto<SparePartMovementDto>>;
}
