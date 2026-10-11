using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// خرائط الفروع من بياناتها (2026-10-11): كل فرع خريطته تلقائياً — لا إعداد يحدد فرعاً مالكاً.
    /// طبقة الأجهزة = المواقع التي فيها تركيبات مسجّلة باسم الفرع (DeviceSite.BranchId)، ثم طبقات الفرع الفعّالة
    /// (1.2.0) = المواقع التي عليها سجلات لكل طبقة. الخريطة تعرض المواقع وأعدادها فقط؛ تفاصيل السجلات بصلاحياتها.
    /// </summary>
    public class MapService : IMapService
    {
        private readonly DataContext _context;

        public MapService(DataContext context)
        {
            _context = context;
        }

        public async Task<BranchMapDto?> GetBranchMapAsync(int branchId)
        {
            var branch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
            if (branch == null) return null;

            var map = new BranchMapDto { BranchId = branch.Id, BranchName = branch.Name };

            var devices = await BuildDevicesLayerAsync(branch.Id);
            if (devices.Sites.Count > 0) map.Layers.Add(devices);
            map.Layers.AddRange(await BuildEntryLayersAsync(branch.Id));

            return map;
        }

        public async Task<List<MapBranchOptionDto>> GetBranchOptionsAsync()
        {
            var withDevices = await _context.DeviceSites.AsNoTracking()
                .Where(ds => ds.BranchId != null)
                .Select(ds => ds.BranchId!.Value)
                .Distinct()
                .ToListAsync();
            var withEntries = await _context.MapEntries.AsNoTracking()
                .Where(e => e.Layer.IsActive)
                .Select(e => e.BranchId)
                .Distinct()
                .ToListAsync();
            var withData = withDevices.Union(withEntries).ToList();

            return await _context.Branches.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new MapBranchOptionDto { Id = b.Id, Name = b.Name, HasMapData = withData.Contains(b.Id) })
                .ToListAsync();
        }

        /// <summary>طبقات الفرع الفعّالة التي عليها سجلات: نقطة لكل موقع بعدد سجلاته في الطبقة</summary>
        private async Task<List<MapLayerDto>> BuildEntryLayersAsync(int branchId)
        {
            var layers = await _context.MapLayers.AsNoTracking()
                .Where(l => l.BranchId == branchId && l.IsActive)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
                .Select(l => new { l.Id, l.Name, l.Color })
                .ToListAsync();
            if (layers.Count == 0) return [];

            var layerIds = layers.Select(l => l.Id).ToList();
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

            var result = new List<MapLayerDto>();
            foreach (var layer in layers)
            {
                var layerSites = counts.Where(c => c.LayerId == layer.Id && sites.ContainsKey(c.SiteId))
                    .Select(c =>
                    {
                        var s = sites[c.SiteId];
                        return new MapSiteDto
                        {
                            Id = s.Id, Name = s.Name, Description = s.Description, GovernorateCode = s.GovernorateCode,
                            GovernorateName = Governorates.NameOf(s.GovernorateCode), Latitude = s.Latitude, Longitude = s.Longitude,
                            EntriesCount = c.Count
                        };
                    })
                    .OrderBy(s => s.Name).ToList();
                if (layerSites.Count == 0) continue;
                result.Add(new MapLayerDto
                {
                    Key = $"layer-{layer.Id}",
                    Kind = "entries",
                    Title = layer.Name,
                    Color = layer.Color,
                    Sites = layerSites,
                    SitesWithoutCoordinates = layerSites.Count(s => s.Latitude == null || s.Longitude == null)
                });
            }
            return result;
        }

        private async Task<MapLayerDto> BuildDevicesLayerAsync(int branchId)
        {
            var installationsPerSite = await _context.DeviceSites.AsNoTracking()
                .Where(ds => ds.BranchId == branchId)
                .GroupBy(ds => ds.SiteId)
                .Select(g => new { SiteId = g.Key, Count = g.Count(), Types = g.Select(x => x.DeviceId).Distinct().Count() })
                .ToDictionaryAsync(x => x.SiteId);

            var siteIds = installationsPerSite.Keys.ToList();
            var sites = await _context.Sites.AsNoTracking()
                .Where(s => siteIds.Contains(s.Id))
                .OrderBy(s => s.Name)
                .Select(s => new MapSiteDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    GovernorateCode = s.GovernorateCode,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude
                }).ToListAsync();

            foreach (var site in sites)
            {
                site.GovernorateName = Governorates.NameOf(site.GovernorateCode);
                var stats = installationsPerSite[site.Id];
                site.InstallationsCount = stats.Count;
                site.DeviceTypesCount = stats.Types;
            }

            return new MapLayerDto
            {
                Key = "devices",
                Kind = "devices",
                Title = "تركيبات الأجهزة",
                Sites = sites,
                SitesWithoutCoordinates = sites.Count(s => s.Latitude == null || s.Longitude == null)
            };
        }
    }
}
