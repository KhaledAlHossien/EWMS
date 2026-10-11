using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// خرائط الفروع من بياناتها (2026-10-11): كل فرع خريطته تلقائياً — لا إعداد يحدد فرعاً مالكاً.
    /// طبقة الأجهزة = المواقع التي فيها تركيبات مسجّلة باسم الفرع (DeviceSite.BranchId).
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

            return map;
        }

        public async Task<List<MapBranchOptionDto>> GetBranchOptionsAsync()
        {
            var withData = await _context.DeviceSites.AsNoTracking()
                .Where(ds => ds.BranchId != null)
                .Select(ds => ds.BranchId!.Value)
                .Distinct()
                .ToListAsync();

            return await _context.Branches.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new MapBranchOptionDto { Id = b.Id, Name = b.Name, HasMapData = withData.Contains(b.Id) })
                .ToListAsync();
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
