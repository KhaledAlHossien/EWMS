using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.Layers.Queries.GetAll
{
    public class GetMapLayersQueryHandler : IRequestHandler<GetMapLayersQuery, List<MapLayerDefinitionDto>>
    {
        private readonly IMapLayerService _layers;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapLayerSupport _support;

        public GetMapLayersQueryHandler(IMapLayerService layers, IUserService users, IUserPermissionService permissions, MapLayerSupport support)
        {
            _layers = layers;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<List<MapLayerDefinitionDto>> Handle(GetMapLayersQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            int? branchId;
            if (viewer.IsOrganizationWide) branchId = request.BranchId;
            else if (viewer.User.BranchId is int own) branchId = own;
            else return [];

            return (await _layers.GetAllAsync(branchId, request.IncludeInactive))
                .Select(l => ToDto(l.Layer, l.Entries, includeHidden: true, _support.SourceTitle(l.Layer.Source))).ToList();
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
