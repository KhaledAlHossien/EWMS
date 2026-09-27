using Domain.Entities;

namespace Application.Interfaces
{
    public interface IOfficeService
    {
        Task<Office?> GetByIdAsync(int id);
        Task<List<Office>> GetAllAsync();
        Task<Office> AddAsync(Office office);
        Task<bool> UpdateAsync(Office office);
        Task<bool> DeleteAsync(Office office);
        Task<bool> SaveChangesAsync();

        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
        Task<bool> HasUsersAsync(int officeId);

        Task<List<Office>> GetByDepartmentAsync(int departmentId);
    }
}
