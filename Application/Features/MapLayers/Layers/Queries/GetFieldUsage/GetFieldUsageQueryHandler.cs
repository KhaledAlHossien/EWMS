using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.MapLayers.Layers.Queries.GetFieldUsage
{
    public class GetFieldUsageQueryHandler : IRequestHandler<GetFieldUsageQuery, FieldUsageDto>
    {
        private readonly IMapLayerService _layers;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapLayerSupport _support;

        public GetFieldUsageQueryHandler(IMapLayerService layers, IUserService users, IUserPermissionService permissions, MapLayerSupport support)
        {
            _layers = layers;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<FieldUsageDto> Handle(GetFieldUsageQuery request, CancellationToken ct)
        {
            var field = await _support.LoadFieldAsync(request.FieldId, await CurrentAsync());
            return new FieldUsageDto { Count = await _layers.FieldUsageAsync(field) };
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
