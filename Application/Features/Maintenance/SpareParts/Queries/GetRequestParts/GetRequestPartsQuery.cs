using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetRequestParts
{
    public record GetRequestPartsQuery(int RequestId) : IRequest<RequestPartsDto>;
}
