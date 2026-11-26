using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.RestoreField
{
    public class RestoreMapLayerFieldCommandHandler : IRequestHandler<RestoreMapLayerFieldCommand, MapLayerDefinitionDto>
    {
        private readonly IMapLayerService _layers;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapLayerSupport _support;

        public RestoreMapLayerFieldCommandHandler(IMapLayerService layers, IUserService users, IUserPermissionService permissions, MapLayerSupport support)
        {
            _layers = layers;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MapLayerDefinitionDto> Handle(RestoreMapLayerFieldCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var field = await _support.LoadFieldAsync(request.FieldId, viewer);
            field.IsHidden = false;
            await _layers.SaveChangesAsync();
            return await _support.DtoAsync(await _support.LoadAsync(field.LayerId, viewer));
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
