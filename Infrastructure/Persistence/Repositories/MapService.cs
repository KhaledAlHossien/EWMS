using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// خرائط الفروع من بياناتها (2026-10-11): كل فرع خريطته تلقائياً — لا إعداد يحدد فرعاً مالكاً.
    /// - مزوّدات بيانات النظام (IMapLayerProvider، أولها تركيبات الأجهزة): طبقة تلقائية بكل بيانات الفرع من المزوّد،
    ///   ما لم يعرّف الفرع طبقاته الخاصة منه (مثل «الكاميرات» بفلتر) فتحلّ محلها.
    /// - طبقات الفرع الفعّالة بترتيبها: اليدوية = المواقع التي عليها سجلات، ومن بيانات النظام = المزوّد بفلتر الطبقة.
    /// الخريطة تعرض المواقع وأعدادها فقط؛ التفاصيل بصلاحياتها في صفحات الميزات.
    /// </summary>
    public class MapService : IMapService
    {
        private static readonly IReadOnlyDictionary<string, List<string>> NoFilter = new Dictionary<string, List<string>>();

        private readonly DataContext _context;
        private readonly IEnumerable<IMapLayerProvider> _providers;

        public MapService(DataContext context, IEnumerable<IMapLayerProvider> providers)
        {
            _context = context;
            _providers = providers;
        }

        public async Task<BranchMapDto?> GetBranchMapAsync(int branchId)
        {
            var branch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
            if (branch == null) return null;

            var map = new BranchMapDto { BranchId = branch.Id, BranchName = branch.Name };
            var layers = await _context.MapLayers.AsNoTracking()
                .Where(l => l.BranchId == branchId && l.IsActive)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .Select(l => new { l.Id, l.Name, l.Color, l.Source, l.SourceFilter })
                .ToListAsync();

            foreach (var provider in _providers)
            {
                if (layers.Any(l => l.Source == provider.Key)) continue;
                var sites = await provider.GetSitesAsync(branchId, NoFilter);
                if (sites.Count > 0) map.Layers.Add(Layer(provider.Key, provider.Key, provider.Title, string.Empty, provider.Unit, sites));
            }

            var entrySites = await EntrySitesAsync(layers.Where(l => l.Source == string.Empty).Select(l => l.Id).ToList());
            foreach (var layer in layers)
            {
                if (layer.Source == string.Empty)
                {
                    if (entrySites.TryGetValue(layer.Id, out var sites) && sites.Count > 0)
                        map.Layers.Add(Layer($"layer-{layer.Id}", "entries", layer.Name, layer.Color, "سجل", sites));
                    continue;
                }
                // مزوّد أُزيل من النظام: تُتجاهل طبقته بدل كسر الخريطة
                var provider = _providers.FirstOrDefault(p => p.Key == layer.Source);
                if (provider == null) continue;
                var providerSites = await provider.GetSitesAsync(branchId, MapLayerRules.ParseFilter(layer.SourceFilter));
                if (providerSites.Count > 0)
                    map.Layers.Add(Layer($"layer-{layer.Id}", provider.Key, layer.Name, layer.Color, provider.Unit, providerSites));
            }
            return map;
        }

        public async Task<List<MapBranchOptionDto>> GetBranchOptionsAsync()
        {
            var withData = new HashSet<int>();
            foreach (var provider in _providers) withData.UnionWith(await provider.BranchesWithDataAsync());
            withData.UnionWith(await _context.MapEntries.AsNoTracking()
                .Where(e => e.Layer.IsActive)
                .Select(e => e.BranchId)
                .Distinct()
                .ToListAsync());
            var ids = withData.ToList();

            return await _context.Branches.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new MapBranchOptionDto { Id = b.Id, Name = b.Name, HasMapData = ids.Contains(b.Id) })
                .ToListAsync();
        }

        private static MapLayerDto Layer(string key, string kind, string title, string color, string unit, List<MapSiteDto> sites) => new()
        {
            Key = key, Kind = kind, Title = title, Color = color, Unit = unit, Sites = sites,
            SitesWithoutCoordinates = sites.Count(s => s.Latitude == null || s.Longitude == null)
        };

        /// <summary>الطبقات اليدوية: لكل طبقة المواقع التي عليها سجلات بعددها</summary>
        private async Task<Dictionary<int, List<MapSiteDto>>> EntrySitesAsync(List<int> layerIds)
        {
            if (layerIds.Count == 0) return [];
            var counts = await _context.MapEntries.AsNoTracking()
                .Where(e => layerIds.Contains(e.LayerId))
                .GroupBy(e => new { e.LayerId, e.SiteId })
                .Select(g => new { g.Key.LayerId, g.Key.SiteId, Count = g.Count() })
                .ToListAsync();
            var siteIds = counts.Select(c => c.SiteId).Distinct().ToList();
            var sites = await _context.Sites.AsNoTracking()
                .Where(s => siteIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name, s.Description, s.GovernorateCode, s.Latitude, s.Longitude })
                .ToDictionaryAsync(s => s.Id);

            return counts.Where(c => sites.ContainsKey(c.SiteId))
                .GroupBy(c => c.LayerId)
                .ToDictionary(g => g.Key, g => g.Select(c =>
                {
                    var s = sites[c.SiteId];
                    return new MapSiteDto
                    {
                        Id = s.Id, Name = s.Name, Description = s.Description, GovernorateCode = s.GovernorateCode,
                        GovernorateName = Governorates.NameOf(s.GovernorateCode), Latitude = s.Latitude, Longitude = s.Longitude,
                        EntriesCount = c.Count, Count = c.Count
                    };
                }).OrderBy(s => s.Name).ToList());
        }
    }
}
