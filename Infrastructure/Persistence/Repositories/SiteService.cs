using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class SiteService : ISiteService
    {
        private readonly DataContext _context;

        public SiteService(DataContext context)
        {
            _context = context;
        }

        /// <summary>العدّادات من تركيبات حدّ المستخدم فقط</summary>
        private IQueryable<SiteWithCounts> WithCounts(IQueryable<Site> sites, InstallationScope scope)
        {
            var visible = _context.DeviceSites.Where(scope.Predicate());
            return sites.AsNoTracking().Select(s => new SiteWithCounts(
                s,
                visible.Count(ds => ds.SiteId == s.Id),
                visible.Count(ds => ds.SiteId == s.Id && ds.Status == InstallationStatus.Active)));
        }

        public async Task<Site?> GetByIdAsync(int id) =>
            await _context.Sites.FirstOrDefaultAsync(s => s.Id == id);

        public async Task<SiteWithCounts?> GetWithCountsAsync(int id, InstallationScope scope) =>
            await WithCounts(_context.Sites.Where(s => s.Id == id), scope).FirstOrDefaultAsync();

        public async Task<List<SiteWithCounts>> GetAllWithCountsAsync(InstallationScope scope) =>
            await WithCounts(_context.Sites.OrderBy(s => s.Name), scope).ToListAsync();

        public async Task<List<NearbySite>> GetNearbyAsync(double latitude, double longitude, double radiusMeters, int? excludeId)
        {
            // تصفية أولية بمربع حول النقطة (درجة العرض ≈ 111 كم) ثم المسافة الدقيقة في الذاكرة
            var delta = radiusMeters / 111_000 * 1.5;
            var candidates = await _context.Sites.AsNoTracking()
                .Where(s => s.Id != excludeId && s.Latitude != null && s.Longitude != null
                    && s.Latitude >= latitude - delta && s.Latitude <= latitude + delta
                    && s.Longitude >= longitude - delta * 1.3 && s.Longitude <= longitude + delta * 1.3)
                .Select(s => new { s.Id, s.Name, Lat = s.Latitude!.Value, Lng = s.Longitude!.Value })
                .ToListAsync();
            return candidates
                .Select(s => new NearbySite(s.Id, s.Name, Math.Round(GeoRules.DistanceMeters(latitude, longitude, s.Lat, s.Lng))))
                .Where(s => s.DistanceMeters <= radiusMeters)
                .OrderBy(s => s.DistanceMeters)
                .ToList();
        }

        public async Task<Dictionary<string, Site>> GetByNamesAsync(IEnumerable<string> names)
        {
            var list = names.Where(n => n.Length > 0).Distinct().ToList();
            var sites = await _context.Sites.AsNoTracking().Where(s => list.Contains(s.Name)).ToListAsync();
            // مقارنة الأسماء في قاعدة البيانات لا تميّز حالة الأحرف — والقاموس كذلك
            return sites.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<Site> AddAsync(Site site)
        {
            await _context.Sites.AddAsync(site);
            await SaveAsync();
            return site;
        }

        public async Task UpdateAsync(Site site, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(site).Property(s => s.RowVersion).OriginalValue = rowVersion;
            await SaveAsync();
        }

        public async Task DeleteAsync(Site site)
        {
            _context.Sites.Remove(site);
            await SaveAsync();
        }

        public async Task<bool> ExistsAsync(int id) => await _context.Sites.AnyAsync(s => s.Id == id);

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null) =>
            await _context.Sites.AnyAsync(s => s.Name == name && s.Id != excludeId);

        public async Task<bool> HasDeviceLinksAsync(int siteId) =>
            await _context.DeviceSites.AnyAsync(ds => ds.SiteId == siteId);

        private Task SaveAsync() => DeviceInventorySave.SaveAsync(_context, "يوجد موقع بنفس الاسم مسبقاً");
    }
}
