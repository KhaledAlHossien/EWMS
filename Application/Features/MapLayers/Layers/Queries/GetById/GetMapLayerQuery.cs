using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Layers.Queries.GetById
{
    public record GetMapLayerQuery(int Id) : IRequest<MapLayerDefinitionDto>;
}
