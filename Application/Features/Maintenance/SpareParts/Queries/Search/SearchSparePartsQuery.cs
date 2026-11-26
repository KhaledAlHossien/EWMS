using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.Search
{
    public record SearchSparePartsQuery(SparePartFilterDto Filter) : IRequest<PagedResultDto<SparePartResponseDto>>;
}
