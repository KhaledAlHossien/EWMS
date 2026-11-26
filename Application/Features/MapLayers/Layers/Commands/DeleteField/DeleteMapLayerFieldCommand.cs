using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.DeleteField
{
    /// <summary>حقل بلا قيم: يُحذف. له قيم: Purge=false يخفيه (تبقى القيم)، Purge=true يحذفه مع قيمه</summary>
    public record DeleteMapLayerFieldCommand(int FieldId, bool Purge) : IRequest<MapLayerDefinitionDto>;
}
