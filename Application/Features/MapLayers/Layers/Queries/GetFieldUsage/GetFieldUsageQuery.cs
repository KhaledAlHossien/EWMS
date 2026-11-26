using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Layers.Queries.GetFieldUsage
{
    public record GetFieldUsageQuery(int FieldId) : IRequest<FieldUsageDto>;
}
