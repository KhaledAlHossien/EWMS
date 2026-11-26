using MediatR;

namespace Application.Features.MapLayers.Entries.Commands.Delete
{
    public record DeleteMapEntryCommand(int Id) : IRequest<Unit>;
}
