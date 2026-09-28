using Domain.Entities;

namespace Application.Interfaces
{
    public interface ISiteService
    {
        Task<Site?> GetByIdAsync(int id);
        Task<List<Site>> GetAllAsync();
        Task<List<Site>> GetByRegionAsync(int regionId);
        Task<Site> AddAsync(Site site);
        Task<bool> UpdateAsync(Site site);
        Task<bool> DeleteAsync(Site site);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> HasDeviceLinksAsync(int siteId);
    }
}
