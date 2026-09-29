using Domain.Entities.Maintenance;

namespace Application.Interfaces
{
    public interface IDamageTypeService
    {
        Task<DamageType?> GetByIdAsync(int id);
        Task<List<DamageType>> GetAllAsync();
        Task<DamageType> AddAsync(DamageType damageType);
        Task<bool> UpdateAsync(DamageType damageType);
        Task<bool> DeleteAsync(DamageType damageType);

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> IsUsedAsync(int id);
    }
}
