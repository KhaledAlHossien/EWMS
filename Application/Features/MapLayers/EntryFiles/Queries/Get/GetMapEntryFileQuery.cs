using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.EntryFiles.Queries.Get
{
    public record GetMapEntryFileQuery(int Id) : IRequest<MapEntryFileContentDto>;
}
