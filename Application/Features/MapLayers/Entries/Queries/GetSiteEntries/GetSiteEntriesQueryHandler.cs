using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.Entries.Queries.GetSiteEntries
{
    public class GetSiteEntriesQueryHandler : IRequestHandler<GetSiteEntriesQuery, SiteEntriesDto>
    {
        private readonly IMapEntryService _entries;
        private readonly IMapLayerService _layers;
        private readonly ISiteService _sites;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public GetSiteEntriesQueryHandler(IMapEntryService entries, IMapLayerService layers, ISiteService sites, IUserService users, IUserPermissionService permissions)
        {
            _entries = entries;
            _layers = layers;
            _sites = sites;
            _users = users;
            _permissions = permissions;
        }

        public async Task<SiteEntriesDto> Handle(GetSiteEntriesQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            if (!await _sites.ExistsAsync(request.SiteId)) throw new KeyNotFoundException("الموقع غير موجود");

            // الطبقات المتاحة للإضافة والعرض: طبقات فرعي الفعّالة، أو كل الفروع لنظرة المؤسسة
            int? branchId = viewer.IsOrganizationWide ? null : viewer.User.BranchId;
            var layers = !viewer.IsOrganizationWide && branchId == null
                ? []
                : (await _layers.GetAllAsync(branchId, includeInactive: false))
                    .Where(l => l.Layer.Source.Length == 0)   // طبقات بيانات النظام لا تُضاف إليها سجلات
                    .Select(l => ToDto(l.Layer, l.Entries, includeHidden: false)).ToList();

            var entries = await _entries.GetForSiteAsync(request.SiteId, EntryScopeFor(viewer));
            var canCreate = viewer.Has("CreateMapEntry") && layers.Count > 0;

            // طبقات السجلات المعروضة وإن كانت معطّلة: عناوين حقولها لعرض القيم (للقراءة فقط — IsActive=false)
            foreach (var layer in entries.Select(e => e.Layer).DistinctBy(l => l.Id))
                if (!layers.Any(l => l.Id == layer.Id)) layers.Add(ToDto(layer, 0, includeHidden: false));

            return new SiteEntriesDto
            {
                Layers = layers,
                Entries = entries.Select(e => ToDto(e, viewer)).ToList(),
                CanCreate = canCreate
            };
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
