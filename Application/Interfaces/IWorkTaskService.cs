using Domain.Entities;

namespace Application.Interfaces
{
    public interface IWorkTaskService
    {
        Task<WorkTask?> GetByIdAsync(int id);                     // مع الفرع + الإسنادات (الموظف/قسمه/مكتبه)
        Task<List<WorkTask>> GetAllAsync(int? branchId = null);
        Task<List<WorkTask>> GetByBranchAsync(int branchId, bool activeOnly);

        /// <summary>مهام الموظف الفعّالة — فقط مهام فرعه الحالي (لو نُقل لفرع آخر لا تظهر مهام الفرع القديم)</summary>
        Task<List<WorkTask>> GetForUserAsync(int userId, int branchId);

        Task<bool> IsAssignedAsync(int workTaskId, int userId);
        Task<bool> ExistsByNameAsync(int branchId, string name, int? excludeId = null);

        Task<WorkTask> AddAsync(WorkTask task);
        Task UpdateAsync(WorkTask task);
        Task DeleteAsync(WorkTask task);
    }
}
