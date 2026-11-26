using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.Layers.Queries.GetAll
{
    /// <summary>BranchId: لنظرة المؤسسة (null = كل الفروع)؛ لغيرها فرع المستخدم دائماً</summary>
    public record GetMapLayersQuery(int? BranchId, bool IncludeInactive) : IRequest<List<MapLayerDefinitionDto>>;
}
