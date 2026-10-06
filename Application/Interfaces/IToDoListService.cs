using Domain.Entities;

namespace Application.Interfaces
{
    public interface IToDoListService
    {
        /// <summary>مع صاحب القائمة</summary>
        Task<ToDoList?> GetByIdAsync(int id);

        /// <summary>قوائم مستخدم واحد (الأحدث أولاً)، أو كل القوائم إن لم يُحدَّد مستخدم</summary>
        Task<List<ToDoList>> GetAllAsync(int? ownerId);

        Task<ToDoList> AddAsync(ToDoList list);
        Task<bool> UpdateAsync(ToDoList list);
        Task<bool> DeleteAsync(ToDoList list);

        /// <summary>الاسم فريد داخل قوائم الصاحب نفسه</summary>
        Task<bool> ExistsByNameAsync(int ownerId, string name, int? excludeId = null);
    }
}
