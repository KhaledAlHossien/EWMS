using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ToDoListService : IToDoListService
    {
        private readonly DataContext _context;

        public ToDoListService(DataContext context)
        {
            _context = context;
        }

        public async Task<ToDoList?> GetByIdAsync(int id) =>
            await _context.ToDoLists.Include(l => l.User)
                .Include(l => l.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)).ThenInclude(i => i.LinkedTask)
                .FirstOrDefaultAsync(l => l.Id == id);

        public async Task<List<ToDoList>> GetAllAsync(int? ownerId)
        {
            var query = _context.ToDoLists.AsQueryable();

            if (ownerId is int id)
                query = query.Where(l => l.UserId == id);

            return await query
                .Include(l => l.User)
                .Include(l => l.Items)
                .OrderByDescending(l => l.IsPinned).ThenByDescending(l => l.Id)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<ToDoList> AddAsync(ToDoList list)
        {
            var result = await _context.ToDoLists.AddAsync(list);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<bool> UpdateAsync(ToDoList list)
        {
            // القائمة محمَّلة ومتتبَّعة مع صاحبها — Update() كان سيعلّم المستخدم كمعدَّل أيضاً
            if (_context.Entry(list).State == EntityState.Detached)
                _context.ToDoLists.Update(list);

            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> DeleteAsync(ToDoList list)
        {
            _context.ToDoLists.Remove(list);
            return (await _context.SaveChangesAsync()) > 0;
        }

        // ===== البنود =====
        public async Task<ToDoItem?> GetItemAsync(int id) =>
            await _context.ToDoItems.Include(i => i.ToDoList).FirstOrDefaultAsync(i => i.Id == id);

        public async Task AddItemAsync(ToDoItem item)
        {
            await _context.ToDoItems.AddAsync(item);
            await _context.SaveChangesAsync();
        }

        public async Task AddItemsAsync(IEnumerable<ToDoItem> items)
        {
            await _context.ToDoItems.AddRangeAsync(items);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ToDoItem>> GetDueItemsAsync(int ownerId, DateTime today) =>
            await _context.ToDoItems
                .Where(i => !i.IsDone && i.DueDate != null && i.DueDate <= today
                            && i.ToDoList.UserId == ownerId && !i.ToDoList.IsArchived)
                .Include(i => i.ToDoList).Include(i => i.LinkedTask)
                .AsNoTracking().ToListAsync();

        public async Task<List<ToDoItem>> GetForRemindersAsync(DateTime tomorrow) =>
            await _context.ToDoItems
                .Where(i => !i.IsDone && i.DueDate != null && i.DueDate <= tomorrow && !i.ToDoList.IsArchived
                            && (i.OverdueNotifiedAt == null || i.DueSoonNotifiedAt == null))
                .Include(i => i.ToDoList)
                .ToListAsync();

        public async Task DeleteItemsAsync(IEnumerable<ToDoItem> items)
        {
            _context.ToDoItems.RemoveRange(items);
            await _context.SaveChangesAsync();
        }

        public Task<int> CountItemsAsync(int listId) => _context.ToDoItems.CountAsync(i => i.ToDoListId == listId);

        public async Task<int> NextSortOrderAsync(int listId) =>
            (await _context.ToDoItems.Where(i => i.ToDoListId == listId).MaxAsync(i => (int?)i.SortOrder) ?? -1) + 1;

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task<bool> ExistsByNameAsync(int ownerId, string name, int? excludeId = null) =>
            await _context.ToDoLists.AnyAsync(l => l.UserId == ownerId && l.Name == name && l.Id != excludeId);
    }
}
