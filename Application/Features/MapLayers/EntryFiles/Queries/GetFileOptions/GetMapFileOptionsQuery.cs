using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.EntryFiles.Queries.GetFileOptions
{
    public record GetMapFileOptionsQuery : IRequest<MapFileOptionsDto>;
}
