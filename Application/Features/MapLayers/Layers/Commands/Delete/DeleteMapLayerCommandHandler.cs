using Application.Common;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.Delete
{
    public class DeleteMapLayerCommandHandler : IRequestHandler<DeleteMapLayerCommand, Unit>
    {
        private readonly IMapLayerService _layers;
        private readonly IMapEntryService _entries;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapLayerSupport _support;

        public DeleteMapLayerCommandHandler(IMapLayerService layers, IMapEntryService entries, IUserService users, IUserPermissionService permissions, MapLayerSupport support)
        {
            _layers = layers;
            _entries = entries;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteMapLayerCommand request, CancellationToken ct)
        {
            var layer = await _support.LoadAsync(request.Id, await CurrentAsync());
            var count = await _entries.CountForLayerAsync(layer.Id);
            if (count > 0)
                throw new InvalidOperationException($"على الطبقة {count} سجلاً — احذف سجلاتها أولاً، أو عطّل الطبقة لإخفائها مع إبقاء سجلاتها");
            await _layers.DeleteAsync(layer);
            return Unit.Value;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
