using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Layers.Queries.GetSources
{
    /// <summary>مزوّدات بيانات النظام وفلاترها من بيانات الفرع (فرعي، أو المختار لنظرة المؤسسة)</summary>
    public record GetMapSourcesQuery(int? BranchId) : IRequest<List<MapSourceDto>>;
}
