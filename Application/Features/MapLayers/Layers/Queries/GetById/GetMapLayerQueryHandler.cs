using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.Layers.Queries.GetById
{
    public class GetMapLayerQueryHandler : IRequestHandler<GetMapLayerQuery, MapLayerDefinitionDto>
    {
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapLayerSupport _support;

        public GetMapLayerQueryHandler(IUserService users, IUserPermissionService permissions, MapLayerSupport support)
        {
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MapLayerDefinitionDto> Handle(GetMapLayerQuery request, CancellationToken ct) =>
            await _support.DtoAsync(await _support.LoadAsync(request.Id, await CurrentAsync()));

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
