using Application.Common;
using Application.DTOs.Response;
using Application.Features.DeviceInventory;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// مزوّد «تركيبات الأجهزة» لخرائط الفروع: المواقع التي فيها تركيبات مسجّلة باسم الفرع (DeviceSite.BranchId)،
    /// مع فلاتر القسم وفئة الجهاز والجهاز والحالة — فيعرّف الفرع طبقات مثل «الكاميرات» أو «المعطّلة» بلا إدخال.
    /// </summary>
    public class DevicesMapLayerProvider : IMapLayerProvider
    {
        public const string ProviderKey = "devices";
        private const string Department = "department", Category = "category", Device = "device", Status = "status";
        private static readonly string[] Keys = [Department, Category, Device, Status];

        private readonly DataContext _context;

        public DevicesMapLayerProvider(DataContext context) => _context = context;

        public string Key => ProviderKey;
        public string Title => "تركيبات الأجهزة";
        public string Unit => "تركيب";
        public IReadOnlyCollection<string> FilterKeys => Keys;

        public async Task<List<MapSourceFilterDto>> GetFiltersAsync(int branchId)
        {
            var departments = await _context.Departments.AsNoTracking()
                .Where(d => d.BranchId == branchId).OrderBy(d => d.Name)
                .Select(d => new MapSourceOptionDto(d.Id.ToString(), d.Name)).ToListAsync();
            var devices = await _context.DeviceSites.AsNoTracking()
                .Where(ds => ds.BranchId == branchId)
                .Select(ds => new { ds.Device.Id, ds.Device.Name, ds.Device.Model, ds.Device.Category })
                .Distinct().ToListAsync();

            return
            [
                new() { Key = Department, Label = "القسم", Options = departments },
                new()
                {
                    Key = Category, Label = "فئة الجهاز",
                    Options = devices.Select(d => d.Category.Trim()).Where(c => c.Length > 0).Distinct().Order()
                        .Select(c => new MapSourceOptionDto(c, c)).ToList()
                },
                new()
                {
                    Key = Device, Label = "الجهاز",
                    Options = devices.OrderBy(d => d.Name).ThenBy(d => d.Model)
                        .Select(d => new MapSourceOptionDto(d.Id.ToString(), string.IsNullOrWhiteSpace(d.Model) ? d.Name : $"{d.Name} ({d.Model})")).ToList()
                },
                new()
                {
                    Key = Status, Label = "الحالة",
                    Options = Enum.GetValues<InstallationStatus>()
                        .Select(s => new MapSourceOptionDto(((int)s).ToString(), DeviceInventoryRules.StatusAr(s))).ToList()
                }
            ];
        }

        public async Task<List<MapSiteDto>> GetSitesAsync(int branchId, IReadOnlyDictionary<string, List<string>> filter)
        {
            var query = _context.DeviceSites.AsNoTracking().Where(ds => ds.BranchId == branchId);
            if (Ints(filter, Department) is { Count: > 0 } departments)
                query = query.Where(ds => ds.DepartmentId != null && departments.Contains(ds.DepartmentId.Value));
            if (filter.TryGetValue(Category, out var categories) && categories.Count > 0)
                query = query.Where(ds => categories.Contains(ds.Device.Category));
            if (Ints(filter, Device) is { Count: > 0 } devices)
                query = query.Where(ds => devices.Contains(ds.DeviceId));
            if (Ints(filter, Status) is { Count: > 0 } statuses)
            {
                var values = statuses.Select(s => (InstallationStatus)s).ToList();
                query = query.Where(ds => values.Contains(ds.Status));
            }

            var perSite = await query
                .GroupBy(ds => ds.SiteId)
                .Select(g => new { SiteId = g.Key, Count = g.Count(), Types = g.Select(x => x.DeviceId).Distinct().Count() })
                .ToDictionaryAsync(x => x.SiteId);
            var siteIds = perSite.Keys.ToList();
            var sites = await _context.Sites.AsNoTracking()
                .Where(s => siteIds.Contains(s.Id))
                .OrderBy(s => s.Name)
                .Select(s => new MapSiteDto
                {
                    Id = s.Id, Name = s.Name, Description = s.Description, GovernorateCode = s.GovernorateCode,
                    Latitude = s.Latitude, Longitude = s.Longitude
                }).ToListAsync();

            foreach (var site in sites)
            {
                site.GovernorateName = Governorates.NameOf(site.GovernorateCode);
                var stats = perSite[site.Id];
                site.InstallationsCount = stats.Count;
                site.DeviceTypesCount = stats.Types;
                site.Count = stats.Count;
            }
            return sites;
        }

        public Task<List<int>> BranchesWithDataAsync() =>
            _context.DeviceSites.AsNoTracking().Where(ds => ds.BranchId != null).Select(ds => ds.BranchId!.Value).Distinct().ToListAsync();

        private static List<int> Ints(IReadOnlyDictionary<string, List<string>> filter, string key) =>
            filter.TryGetValue(key, out var values) ? values.Select(v => int.TryParse(v, out var n) ? n : 0).Where(n => n > 0).ToList() : [];
    }
}
