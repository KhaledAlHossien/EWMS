using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>موقع مع عدد تركيباته (كلها، والتي تعمل)</summary>
    public sealed record SiteWithCounts(Site Site, int Installations, int Active);

    public interface ISiteService
    {
        Task<Site?> GetByIdAsync(int id);
        Task<SiteWithCounts?> GetWithCountsAsync(int id);
        Task<List<SiteWithCounts>> GetAllWithCountsAsync();
        /// <summary>المواقع بأسمائها (مطابقة تامة بلا فراغات زائدة) — لاستيراد Excel</summary>
        Task<Dictionary<string, Site>> GetByNamesAsync(IEnumerable<string> names);

        Task<Site> AddAsync(Site site);
        /// <summary>يُرفض (InvalidOperationException) إن تغيّر السجل منذ أن قُرئت rowVersion</summary>
        Task UpdateAsync(Site site, byte[]? rowVersion);
        Task DeleteAsync(Site site);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> HasDeviceLinksAsync(int siteId);
    }
}
