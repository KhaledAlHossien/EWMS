using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers.EntryFiles;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.Entries.Commands.Create
{
    public class CreateMapEntryCommandHandler : IRequestHandler<CreateMapEntryCommand, MapEntryDto>
    {
        private readonly IMapEntryService _entries;
        private readonly IMapEntryFileService _files;
        private readonly IMapLayerService _layers;
        private readonly ISiteService _sites;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapEntrySupport _support;

        public CreateMapEntryCommandHandler(IMapEntryService entries, IMapEntryFileService files, IMapLayerService layers, ISiteService sites, IUserService users, IUserPermissionService permissions, MapEntrySupport support)
        {
            _entries = entries;
            _files = files;
            _layers = layers;
            _sites = sites;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MapEntryDto> Handle(CreateMapEntryCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var dto = request.Dto;
            var layer = await _layers.GetByIdAsync(dto.LayerId) ?? throw new KeyNotFoundException("الطبقة غير موجودة");
            EnsureLayer(viewer, layer);
            if (layer.Source.Length > 0)
                throw new InvalidOperationException("هذه الطبقة من بيانات النظام — نقاطها تُقرأ من جداولها ولا تُضاف إليها سجلات يدوياً");
            if (!layer.IsActive) throw new InvalidOperationException("الطبقة معطّلة — لا تُضاف إليها سجلات");
            if (!await _sites.ExistsAsync(dto.SiteId)) throw new KeyNotFoundException("الموقع غير موجود");
            if (!layer.AllowMultiple && await _entries.ExistsAsync(layer.Id, dto.SiteId, null))
                throw new InvalidOperationException($"لهذا الموقع سجل في طبقة «{layer.Name}» — عدّله بدل إضافة سجل آخر");

            var entry = new MapEntry
            {
                LayerId = layer.Id,
                SiteId = dto.SiteId,
                BranchId = layer.BranchId,
                DepartmentId = await _support.DepartmentAsync(viewer, layer, dto.DepartmentId),
                Values = NormalizeValues(layer, dto.Values, null),
                CreatedByUserId = viewer.Id,
                CreatedAt = DateTime.UtcNow
            };
            var files = await MapEntryFiles.PlanAsync(layer, null, dto.Values, viewer, _files);
            await _entries.AddAsync(entry, files);
            return await DtoAsync(entry.Id, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<MapEntryDto> DtoAsync(int id, Viewer viewer) =>
            ToDto(await _entries.GetByIdAsync(id) ?? throw new KeyNotFoundException("السجل غير موجود"), viewer);
    }
}
