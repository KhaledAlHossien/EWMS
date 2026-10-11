using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>موقع مع عدد تركيباته (كلها، والتي تعمل) — ضمن حدّ تركيبات المستخدم</summary>
    public sealed record SiteWithCounts(Site Site, int Installations, int Active);

    /// <summary>موقع قريب من نقطة (تنبيه التكرار عند إضافة موقع)</summary>
    public sealed record NearbySite(int Id, string Name, double DistanceMeters);

    public interface ISiteService
    {
        Task<Site?> GetByIdAsync(int id);
        /// <summary>العدّادات تشمل تركيبات حدّ المستخدم فقط (scope)، فلا تظهر أعداد فروع أخرى</summary>
        Task<SiteWithCounts?> GetWithCountsAsync(int id, InstallationScope scope);
        Task<List<SiteWithCounts>> GetAllWithCountsAsync(InstallationScope scope);
        /// <summary>المواقع بأسمائها (مطابقة تامة بلا فراغات زائدة) — لاستيراد Excel</summary>
        Task<Dictionary<string, Site>> GetByNamesAsync(IEnumerable<string> names);
        /// <summary>المواقع ذات الإحداثيات ضمن المسافة (بالمتر) من النقطة، الأقرب أولاً</summary>
        Task<List<NearbySite>> GetNearbyAsync(double latitude, double longitude, double radiusMeters, int? excludeId);

        Task<Site> AddAsync(Site site);
        /// <summary>يُرفض (InvalidOperationException) إن تغيّر السجل منذ أن قُرئت rowVersion</summary>
        Task UpdateAsync(Site site, byte[]? rowVersion);
        Task DeleteAsync(Site site);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> HasDeviceLinksAsync(int siteId);
    }
}
