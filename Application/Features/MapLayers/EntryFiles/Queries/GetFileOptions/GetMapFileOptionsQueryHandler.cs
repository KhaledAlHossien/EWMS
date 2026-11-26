using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.EntryFiles.Queries.GetFileOptions
{
    public class GetMapFileOptionsQueryHandler : IRequestHandler<GetMapFileOptionsQuery, MapFileOptionsDto>
    {
        private readonly IMapLayerSettings _settings;

        public GetMapFileOptionsQueryHandler(IMapLayerSettings settings)
        {
            _settings = settings;
        }

        public Task<MapFileOptionsDto> Handle(GetMapFileOptionsQuery request, CancellationToken ct) => Task.FromResult(new MapFileOptionsDto
        {
            MaxFileMegabytes = _settings.MaxFileMegabytes,
            MaxFilesPerField = MapFileRules.MaxFilesPerField,
            Kinds = MapFileRules.Kinds.Select(k => new MapFileKindDto { Key = k.Key, Label = k.Label, Formats = k.Formats, Extensions = [.. k.Extensions] }).ToList()
        });
    }
}
