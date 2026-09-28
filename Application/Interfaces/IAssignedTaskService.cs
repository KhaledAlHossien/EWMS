using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Interfaces
{
    public interface IAssignedTaskService
    {
        /// <summary>مع كل التفاصيل: الجهة، المُسنِد، الأصل، المهام الفرعية، السجل</summary>
        Task<AssignedTask?> GetByIdAsync(int id);

        /// <summary>بطاقات اللوحة (مع المهام الفرعية وتعليقات فقط من السجل لحساب العدد)</summary>
        Task<List<AssignedTask>> GetBoardAsync(Expression<Func<AssignedTask, bool>> filter);

        Task AddAsync(AssignedTask task);
        Task AddActivityAsync(AssignedTaskActivity activity);
        Task SaveChangesAsync();
        Task DeleteAsync(AssignedTask task);

        // حراسة الحذف (العلاقات Restrict)
        Task<bool> ExistsForDepartmentAsync(int departmentId);
        Task<bool> ExistsForOfficeAsync(int officeId);
        Task<bool> ExistsForUserAsync(int userId);
    }
}
