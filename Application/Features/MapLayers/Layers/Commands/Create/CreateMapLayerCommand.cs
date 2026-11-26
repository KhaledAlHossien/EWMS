using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.Create
{
    public record CreateMapLayerCommand(MapLayerRequestDto Dto) : IRequest<MapLayerDefinitionDto>;
}
