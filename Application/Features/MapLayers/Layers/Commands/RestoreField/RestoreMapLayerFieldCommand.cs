using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.RestoreField
{
    public record RestoreMapLayerFieldCommand(int FieldId) : IRequest<MapLayerDefinitionDto>;
}
