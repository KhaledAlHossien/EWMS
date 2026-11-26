using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.Layers.Queries.GetSources
{
    public class GetMapSourcesQueryHandler : IRequestHandler<GetMapSourcesQuery, List<MapSourceDto>>
    {
        private readonly IEnumerable<IMapLayerProvider> _providers;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public GetMapSourcesQueryHandler(IEnumerable<IMapLayerProvider> providers, IUserService users, IUserPermissionService permissions)
        {
            _providers = providers;
            _users = users;
            _permissions = permissions;
        }

        public async Task<List<MapSourceDto>> Handle(GetMapSourcesQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var branchId = viewer.IsOrganizationWide && request.BranchId is int chosen ? chosen : viewer.User.BranchId;
            if (branchId is not int b) return [];

            var result = new List<MapSourceDto>();
            foreach (var provider in _providers)
                result.Add(new MapSourceDto { Key = provider.Key, Title = provider.Title, Unit = provider.Unit, Filters = await provider.GetFiltersAsync(b) });
            return result;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
