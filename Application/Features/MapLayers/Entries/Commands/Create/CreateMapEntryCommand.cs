using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Entries.Commands.Create
{
    public record CreateMapEntryCommand(MapEntryRequestDto Dto) : IRequest<MapEntryDto>;
}
