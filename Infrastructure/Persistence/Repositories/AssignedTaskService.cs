using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AssignedTaskService : IAssignedTaskService
    {
        private readonly DataContext _context;

        public AssignedTaskService(DataContext context)
        {
            _context = context;
        }

        public async Task<AssignedTask?> GetByIdAsync(int id)
        {
            return await _context.AssignedTasks
                .Include(t => t.Branch)
                .Include(t => t.Department)
                .Include(t => t.Office)
                .Include(t => t.AssigneeUser)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ParentTask).ThenInclude(p => p!.Attachments)
                .Include(t => t.ClaimedByUser)
                .Include(t => t.Attachments).ThenInclude(a => a.UploadedByUser)
                .Include(t => t.ChecklistItems)
                .Include(t => t.SubTasks).ThenInclude(s => s.Department)
                .Include(t => t.SubTasks).ThenInclude(s => s.Office)
                .Include(t => t.SubTasks).ThenInclude(s => s.AssigneeUser)
                .Include(t => t.SubTasks).ThenInclude(s => s.CreatedByUser)
                .Include(t => t.SubTasks).ThenInclude(s => s.Attachments)
                .Include(t => t.SubTasks).ThenInclude(s => s.ChecklistItems)
                .Include(t => t.SubTasks).ThenInclude(s => s.ClaimedByUser)
                .Include(t => t.Activities).ThenInclude(a => a.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<AssignedTask>> GetBoardAsync(Expression<Func<AssignedTask, bool>> filter)
        {
            return await _context.AssignedTasks.AsNoTracking()
                .Where(filter)
                .Include(t => t.Branch)
                .Include(t => t.Department)
                .Include(t => t.Office)
                .Include(t => t.AssigneeUser)
                .Include(t => t.CreatedByUser)
                .Include(t => t.ParentTask)
                .Include(t => t.SubTasks)
                .Include(t => t.ClaimedByUser)
                .Include(t => t.Attachments)
                .Include(t => t.ChecklistItems)
                .Include(t => t.Activities.Where(a => a.Type == AssignedTaskActivityType.Comment))
                .AsSplitQuery()
                .OrderByDescending(t => t.Priority).ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(AssignedTask task)
        {
            await _context.AssignedTasks.AddAsync(task);
            await SaveAsync();
        }

        public async Task AddActivityAsync(AssignedTaskActivity activity)
        {
            await _context.AssignedTaskActivities.AddAsync(activity);
            await SaveAsync();
        }

        public Task SaveChangesAsync() => SaveAsync();

        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // عدّل مستخدم آخر المهمة بين تحميلها وحفظها (RowVersion)
                throw new InvalidOperationException("تم تعديل هذه المهمة من مستخدم آخر للتو، حدّث الصفحة وحاول مجدداً");
            }
        }

        // ═════════ المرفقات ═════════
        public Task<AssignedTaskAttachment?> GetAttachmentAsync(int id) =>
            _context.AssignedTaskAttachments
                .Include(a => a.Content)
                .Include(a => a.AssignedTask)
                .FirstOrDefaultAsync(a => a.Id == id);

        public async Task AddAttachmentsAsync(IEnumerable<AssignedTaskAttachment> attachments)
        {
            _context.AssignedTaskAttachments.AddRange(attachments);
            await SaveAsync();
        }

        public async Task DeleteAttachmentAsync(AssignedTaskAttachment attachment)
        {
            _context.AssignedTaskAttachments.Remove(attachment);
            await SaveAsync();
        }

        public Task<int> CountAttachmentsAsync(int taskId) =>
            _context.AssignedTaskAttachments.CountAsync(a => a.AssignedTaskId == taskId);

        public Task<List<AssignedTask>> GetChildrenAsync(int parentTaskId) =>
            _context.AssignedTasks.AsNoTracking().Where(t => t.ParentTaskId == parentTaskId).ToListAsync();

        // ═════════ قائمة التحقق ═════════
        public Task<AssignedTaskChecklistItem?> GetChecklistItemAsync(int id) =>
            _context.AssignedTaskChecklistItems.Include(i => i.AssignedTask).FirstOrDefaultAsync(i => i.Id == id);

        public async Task AddChecklistItemAsync(AssignedTaskChecklistItem item)
        {
            item.SortOrder = (await _context.AssignedTaskChecklistItems
                .Where(i => i.AssignedTaskId == item.AssignedTaskId).MaxAsync(i => (int?)i.SortOrder) ?? 0) + 1;
            _context.AssignedTaskChecklistItems.Add(item);
            await SaveAsync();
        }

        public async Task DeleteChecklistItemAsync(AssignedTaskChecklistItem item)
        {
            _context.AssignedTaskChecklistItems.Remove(item);
            await SaveAsync();
        }

        public Task<int> CountChecklistAsync(int taskId) =>
            _context.AssignedTaskChecklistItems.CountAsync(i => i.AssignedTaskId == taskId);

        // ═════════ التذكيرات ═════════
        public Task<List<AssignedTask>> GetForRemindersAsync(DateTime tomorrow) =>
            _context.AssignedTasks
                .Where(t => t.Status != AssignedTaskStatus.Done && t.DueDate != null && t.DueDate.Value.Date <= tomorrow.Date
                    && (t.DueSoonNotifiedAt == null || t.OverdueNotifiedAt == null))
                .ToListAsync();

        public async Task DeleteAsync(AssignedTask task)
        {
            _context.AssignedTasks.Remove(task);
            await SaveAsync();
        }

        public Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            _context.AssignedTasks.AnyAsync(t => t.DepartmentId == departmentId);

        public Task<bool> ExistsForOfficeAsync(int officeId) =>
            _context.AssignedTasks.AnyAsync(t => t.OfficeId == officeId);

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.AssignedTasks.AnyAsync(t => t.CreatedByUserId == userId || t.AssigneeUserId == userId || t.ClaimedByUserId == userId)
            || await _context.AssignedTaskActivities.AnyAsync(a => a.UserId == userId)
            || await _context.AssignedTaskAttachments.AnyAsync(a => a.UploadedByUserId == userId)
            || await _context.AssignedTaskTemplates.AnyAsync(t => t.OwnerUserId == userId)
            || await _context.AssignedTaskRecurrences.AnyAsync(r => r.OwnerUserId == userId)
            || await _context.AssignedTaskLinks.AnyAsync(l => l.CreatedByUserId == userId);
    }
}
