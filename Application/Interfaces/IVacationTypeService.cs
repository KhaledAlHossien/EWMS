using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IVacationTypeService
    {
        Task<List<VacationType>> GetAllAsync();
        Task<VacationType?> GetByIdAsync(int id);
        Task AddAsync(VacationType vacationType);
        Task UpdateAsync(VacationType vacationType);
        Task<bool> DeleteAsync(VacationType vacationType);
        Task<bool> SaveChangesAsync();
        Task<bool> ExistsAsync(int id);
        Task<bool> IsNameUniqueAsync(string name, int? excludeId = null);
        Task<bool> IsUsedAsync(int vacationTypeId);  // هل مستخدم في إجازة؟
    }
}
