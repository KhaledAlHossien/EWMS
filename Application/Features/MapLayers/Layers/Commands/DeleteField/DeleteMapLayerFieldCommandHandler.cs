using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.DeleteField
{
    public class DeleteMapLayerFieldCommandHandler : IRequestHandler<DeleteMapLayerFieldCommand, MapLayerDefinitionDto>
    {
        private readonly IMapLayerService _layers;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapLayerSupport _support;

        public DeleteMapLayerFieldCommandHandler(IMapLayerService layers, IUserService users, IUserPermissionService permissions, MapLayerSupport support)
        {
            _layers = layers;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MapLayerDefinitionDto> Handle(DeleteMapLayerFieldCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var field = await _support.LoadFieldAsync(request.FieldId, viewer);
            var layerId = field.LayerId;
            var used = await _layers.FieldUsageAsync(field) > 0;

            if (used && !request.Purge)
            {
                field.IsHidden = true;              // إخفاء: القيم محفوظة ويمكن إعادته
                await _layers.SaveChangesAsync();
            }
            else
            {
                await _layers.PurgeFieldAsync(field);   // بلا قيم، أو حذف نهائي مع قيمه
            }
            return await _support.DtoAsync(await _support.LoadAsync(layerId, viewer));
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
