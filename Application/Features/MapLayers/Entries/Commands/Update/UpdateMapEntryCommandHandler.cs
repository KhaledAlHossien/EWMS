using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers.EntryFiles;
using Application.Interfaces;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.Entries.Commands.Update
{
    public class UpdateMapEntryCommandHandler : IRequestHandler<UpdateMapEntryCommand, MapEntryDto>
    {
        private readonly IMapEntryService _entries;
        private readonly IMapEntryFileService _files;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapEntrySupport _support;

        public UpdateMapEntryCommandHandler(IMapEntryService entries, IMapEntryFileService files, IUserService users, IUserPermissionService permissions, MapEntrySupport support)
        {
            _entries = entries;
            _files = files;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MapEntryDto> Handle(UpdateMapEntryCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var entry = await _support.LoadAsync(request.Id, viewer);
            if (!entry.Layer.IsActive) throw new InvalidOperationException("الطبقة معطّلة — سجلاتها للقراءة فقط");

            entry.Values = NormalizeValues(entry.Layer, request.Dto.Values, entry.Values);
            var files = await MapEntryFiles.PlanAsync(entry.Layer, entry, request.Dto.Values, viewer, _files);
            if (request.Dto.DepartmentId is int requested && requested != entry.DepartmentId)
                entry.DepartmentId = await _support.DepartmentAsync(viewer, entry.Layer, requested);
            entry.UpdatedByUserId = viewer.Id;
            entry.UpdatedAt = DateTime.UtcNow;
            await _entries.UpdateAsync(entry, request.Dto.RowVersion, files);
            return await DtoAsync(entry.Id, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<MapEntryDto> DtoAsync(int id, Viewer viewer) =>
            ToDto(await _entries.GetByIdAsync(id) ?? throw new KeyNotFoundException("السجل غير موجود"), viewer);
    }
}
