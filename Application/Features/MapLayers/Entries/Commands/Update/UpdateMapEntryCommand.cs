using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Entries.Commands.Update
{
    public record UpdateMapEntryCommand(int Id, MapEntryRequestDto Dto) : IRequest<MapEntryDto>;
}
