using Application.Common;
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
                    GovernorateCode = s.GovernorateCode,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude
                }).ToListAsync();

            foreach (var site in sites)
            {
                site.GovernorateName = Governorates.NameOf(site.GovernorateCode);

                if (installationsPerSite.TryGetValue(site.Id, out var stats))
                {
                    site.InstallationsCount = stats.Count;
                    site.DeviceTypesCount = stats.Types;
                }
            }

            return new DevicesMapLayerDto
            {
                Sites = sites,
                SitesWithoutCoordinates = sites.Count(s => s.Latitude == null || s.Longitude == null)
            };
        }
    }
}
