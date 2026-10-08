using Domain.Entities;

namespace Application.Interfaces
{
    public interface IToDoListService
    {
        /// <summary>مع صاحب القائمة وبنودها (مرتبة) والمهام المرتبطة بها</summary>
        Task<ToDoList?> GetByIdAsync(int id);

        /// <summary>قوائم مستخدم واحد (المثبّتة أولاً ثم الأحدث)، أو كل القوائم إن لم يُحدَّد مستخدم — مع البنود للأعداد</summary>
        Task<List<ToDoList>> GetAllAsync(int? ownerId);

        Task<ToDoList> AddAsync(ToDoList list);
        Task<bool> UpdateAsync(ToDoList list);
        Task<bool> DeleteAsync(ToDoList list);

        /// <summary>الاسم فريد داخل قوائم الصاحب نفسه</summary>
        Task<bool> ExistsByNameAsync(int ownerId, string name, int? excludeId = null);

        // ===== البنود =====
        /// <summary>البند مع قائمته وصاحبها (متتبَّع للتعديل)</summary>
        Task<ToDoItem?> GetItemAsync(int id);
        Task AddItemAsync(ToDoItem item);
        Task AddItemsAsync(IEnumerable<ToDoItem> items);
        Task DeleteItemsAsync(IEnumerable<ToDoItem> items);
        Task<int> CountItemsAsync(int listId);
        /// <summary>رقم ترتيب بعد آخر بند</summary>
        Task<int> NextSortOrderAsync(int listId);
        Task SaveChangesAsync();

        /// <summary>«مهامي اليوم»: غير المنجزة المستحقة حتى اليوم في قوائم المستخدم غير المؤرشفة (مع القائمة والمهمة المرتبطة)</summary>
        Task<List<ToDoItem>> GetDueItemsAsync(int ownerId, DateTime today);

        /// <summary>للتذكيرات: غير المنجزة التي موعدها حتى tomorrow ولم يكتمل تذكيرها، في قوائم غير مؤرشفة (مع القائمة)</summary>
        Task<List<ToDoItem>> GetForRemindersAsync(DateTime tomorrow);
    }
}
