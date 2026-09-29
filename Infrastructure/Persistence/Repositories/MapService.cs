using Application.DTOs.Response;
using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence.Repositories
{
    public class MapService : IMapService
    {
        private readonly DataContext _context;
        private readonly int _devicesOwnerDepartmentId;

        public MapService(DataContext context, IConfiguration configuration)
        {
            _context = context;
            _devicesOwnerDepartmentId = configuration.GetValue<int>("DeviceInventory:OwnerDepartmentId");
        }

        public async Task<BranchMapDto?> GetBranchMapAsync(int branchId)
        {
            var branch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
            if (branch == null) return null;

            var map = new BranchMapDto { BranchId = branch.Id, BranchName = branch.Name };

            if (await DevicesBranchIdAsync() == branch.Id)
                map.DevicesLayer = await BuildDevicesLayerAsync();

            return map;
        }

        public async Task<List<MapBranchOptionDto>> GetBranchOptionsAsync()
        {
            var devicesBranchId = await DevicesBranchIdAsync();
            return await _context.Branches.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new MapBranchOptionDto { Id = b.Id, Name = b.Name, HasMapData = b.Id == devicesBranchId })
                .ToListAsync();
        }

        /// <summary>طبقة توثيق الأجهزة تخص فرع القسم المالك لها (قسم العمليات → الفرع التقني)</summary>
        private Task<int?> DevicesBranchIdAsync() => _context.Departments
            .Where(d => d.Id == _devicesOwnerDepartmentId)
            .Select(d => (int?)d.BranchId)
            .FirstOrDefaultAsync();

        private async Task<DevicesMapLayerDto> BuildDevicesLayerAsync()
        {
            var installationsPerSite = await _context.DeviceSites.AsNoTracking()
                .GroupBy(ds => ds.SiteId)
                .Select(g => new { SiteId = g.Key, Count = g.Count(), Types = g.Select(x => x.DeviceId).Distinct().Count() })
                .ToDictionaryAsync(x => x.SiteId);

            var sites = await _context.Sites.AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new MapSiteDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    RegionId = s.RegionId,
                    RegionName = s.Region.Name,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude
                }).ToListAsync();

            foreach (var site in sites)
            {
                if (installationsPerSite.TryGetValue(site.Id, out var stats))
                {
                    site.InstallationsCount = stats.Count;
                    site.DeviceTypesCount = stats.Types;
                }
            }

            var regions = await _context.Regions.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new MapRegionDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    Latitude = r.Latitude,
                    Longitude = r.Longitude
                }).ToListAsync();

            foreach (var region in regions)
            {
                var regionSites = sites.Where(s => s.RegionId == region.Id).ToList();
                region.SitesCount = regionSites.Count;
                region.InstallationsCount = regionSites.Sum(s => s.InstallationsCount);
            }

            return new DevicesMapLayerDto
            {
                Regions = regions,
                Sites = sites,
                RegionsWithoutCoordinates = regions.Count(r => r.Latitude == null || r.Longitude == null),
                SitesWithoutCoordinates = sites.Count(s => s.Latitude == null || s.Longitude == null)
            };
        }
    }
}
