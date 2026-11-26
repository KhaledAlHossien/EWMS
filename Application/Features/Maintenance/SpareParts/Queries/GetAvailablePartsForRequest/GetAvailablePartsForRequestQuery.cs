using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetAvailablePartsForRequest
{
    public record GetAvailablePartsForRequestQuery(int RequestId, string? Search) : IRequest<List<SparePartResponseDto>>;
}
