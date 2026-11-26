using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetById
{
    public record GetSparePartByIdQuery(int Id) : IRequest<SparePartResponseDto>;
}
