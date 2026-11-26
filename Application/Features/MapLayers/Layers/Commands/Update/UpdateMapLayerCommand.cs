using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.Update
{
    public record UpdateMapLayerCommand(int Id, MapLayerRequestDto Dto) : IRequest<MapLayerDefinitionDto>;
}
