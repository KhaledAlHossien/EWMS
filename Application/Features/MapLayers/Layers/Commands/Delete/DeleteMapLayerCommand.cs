using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.Delete
{
    public record DeleteMapLayerCommand(int Id) : IRequest<Unit>;
}
