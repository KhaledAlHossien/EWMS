using Domain.Entities;

namespace Application.Interfaces
{
    public interface IRegionService
    {
        Task<Region?> GetByIdAsync(int id);
        Task<List<Region>> GetAllAsync();
        Task<Region> AddAsync(Region region);
        Task<bool> UpdateAsync(Region region);
        Task<bool> DeleteAsync(Region region);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> HasSitesAsync(int regionId);
    }
}
