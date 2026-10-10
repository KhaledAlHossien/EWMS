using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class AssignedTaskPlanningService : IAssignedTaskPlanningService
    {
        private readonly DataContext _context;

        public AssignedTaskPlanningService(DataContext context) => _context = context;

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        // ===== القوالب =====
        public async Task<List<AssignedTaskTemplate>> GetTemplatesAsync(int ownerUserId) =>
            await _context.AssignedTaskTemplates.AsNoTracking()
                .Where(t => t.OwnerUserId == ownerUserId)
                .Include(t => t.Items.OrderBy(i => i.SortOrder))
                .OrderBy(t => t.Name).ToListAsync();

        public async Task<AssignedTaskTemplate?> GetTemplateAsync(int id) =>
            await _context.AssignedTaskTemplates.Include(t => t.Items.OrderBy(i => i.SortOrder)).FirstOrDefaultAsync(t => t.Id == id);

        public async Task AddTemplateAsync(AssignedTaskTemplate template)
        {
            await _context.AssignedTaskTemplates.AddAsync(template);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteTemplateAsync(AssignedTaskTemplate template)
        {
            _context.AssignedTaskTemplates.Remove(template);
            await _context.SaveChangesAsync();
        }

        public Task<bool> TemplateNameExistsAsync(int ownerUserId, string name, int? excludeId = null) =>
            _context.AssignedTaskTemplates.AnyAsync(t => t.OwnerUserId == ownerUserId && t.Name == name && t.Id != excludeId);

        public Task<bool> TemplateInUseAsync(int templateId) =>
            _context.AssignedTaskRecurrences.AnyAsync(r => r.TemplateId == templateId);

        public async Task ReplaceTemplateItemsAsync(AssignedTaskTemplate template, IEnumerable<string> items)
        {
            _context.AssignedTaskTemplateItems.RemoveRange(template.Items);
            template.Items.Clear();
            var order = 0;
            foreach (var text in items) template.Items.Add(new AssignedTaskTemplateItem { Text = text, SortOrder = order++ });
            await _context.SaveChangesAsync();
        }

        // ===== المهام الدورية =====
        public async Task<List<AssignedTaskRecurrence>> GetRecurrencesAsync(int ownerUserId) =>
            await _context.AssignedTaskRecurrences.AsNoTracking()
                .Where(r => r.OwnerUserId == ownerUserId)
                .Include(r => r.Template).ThenInclude(t => t.Items)
                .OrderByDescending(r => r.IsActive).ThenBy(r => r.NextRunDate).ThenBy(r => r.Id).ToListAsync();

        public async Task<AssignedTaskRecurrence?> GetRecurrenceAsync(int id) =>
            await _context.AssignedTaskRecurrences.Include(r => r.Template).ThenInclude(t => t.Items).FirstOrDefaultAsync(r => r.Id == id);

        public async Task AddRecurrenceAsync(AssignedTaskRecurrence recurrence)
        {
            await _context.AssignedTaskRecurrences.AddAsync(recurrence);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRecurrenceAsync(AssignedTaskRecurrence recurrence)
        {
            _context.AssignedTaskRecurrences.Remove(recurrence);
            await _context.SaveChangesAsync();
        }

        // بلا تتبّع: RunScheduledAsync يعيد قراءة التكرار داخل القفل (نسخة متتبَّعة قديمة كانت ستُخفي ما حفظته نسخة أخرى)
        public async Task<List<AssignedTaskRecurrence>> GetDueRecurrencesAsync(DateTime today) =>
            await _context.AssignedTaskRecurrences.AsNoTracking()
                .Where(r => r.IsActive && r.NextRunDate != null && r.NextRunDate <= today)
                .Include(r => r.Template).ThenInclude(t => t.Items)
                .Include(r => r.OwnerUser)
                .OrderBy(r => r.Id).ToListAsync();

        public async Task<bool> RunScheduledAsync(int recurrenceId, DateTime expectedNextRun, Func<AssignedTaskRecurrence, Task> action)
        {
            // قفل تطبيقي على التكرار داخل معاملة (مثل اعتماد الإجازات): نسختان من الخادم لا تنفّذان الموعد نفسه،
            // وتعطّل الخادم قبل الـ Commit يُرجع كل شيء (المهمة والموعد) فيُنفَّذ الموعد مرة واحدة في الدورة التالية
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.Database.ExecuteSqlRawAsync(@"
DECLARE @r int;
EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @r < 0 THROW 50002, N'task recurrence lock timeout', 1;", $"task-recurrence-{recurrenceId}");

            var recurrence = await _context.AssignedTaskRecurrences
                .Include(r => r.Template).ThenInclude(t => t.Items)
                .Include(r => r.OwnerUser)
                .FirstOrDefaultAsync(r => r.Id == recurrenceId);
            if (recurrence == null || !recurrence.IsActive || recurrence.NextRunDate != expectedNextRun)
                return false;

            try
            {
                await action(recurrence);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                // المعاملة تُرجع عند التخلص منها؛ وما بقي متتبَّعاً من المحاولة (المهمة وسجلها) لا يُحفظ لاحقاً بالخطأ
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task<bool> StopRecurrenceAsync(int recurrenceId, string error)
        {
            var recurrence = await _context.AssignedTaskRecurrences.FirstOrDefaultAsync(r => r.Id == recurrenceId);
            if (recurrence == null || !recurrence.IsActive) return false;
            recurrence.IsActive = false;
            recurrence.LastError = error;
            await _context.SaveChangesAsync();
            return true;
        }

        // ===== الروابط =====
        public async Task<List<AssignedTaskLink>> GetLinksAsync(int taskId) =>
            await _context.AssignedTaskLinks.AsNoTracking()
                .Where(l => l.AssignedTaskId == taskId)
                .Include(l => l.CreatedByUser)
                .OrderBy(l => l.CreatedAt).ToListAsync();

        public async Task<AssignedTaskLink?> GetLinkAsync(int id) =>
            await _context.AssignedTaskLinks.Include(l => l.AssignedTask).FirstOrDefaultAsync(l => l.Id == id);

        public async Task AddLinkAsync(AssignedTaskLink link)
        {
            await _context.AssignedTaskLinks.AddAsync(link);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteLinkAsync(AssignedTaskLink link)
        {
            _context.AssignedTaskLinks.Remove(link);
            await _context.SaveChangesAsync();
        }

        public Task<bool> LinkExistsAsync(int taskId, TaskLinkType type, int entityId) =>
            _context.AssignedTaskLinks.AnyAsync(l => l.AssignedTaskId == taskId && l.EntityType == type && l.EntityId == entityId);

        public Task<int> CountLinksAsync(int taskId) => _context.AssignedTaskLinks.CountAsync(l => l.AssignedTaskId == taskId);

        public async Task<List<int>> GetTaskIdsByLinkAsync(TaskLinkType type, int entityId) =>
            await _context.AssignedTaskLinks.Where(l => l.EntityType == type && l.EntityId == entityId)
                .Select(l => l.AssignedTaskId).Distinct().ToListAsync();

        // ===== الإحصائيات =====
        public async Task<List<TaskStatsRow>> GetStatsRowsAsync(Expression<Func<AssignedTask, bool>> filter, DateTime from, DateTime to) =>
            await _context.AssignedTasks.AsNoTracking()
                .Where(filter).Where(t => t.CreatedAt >= from && t.CreatedAt < to)
                .Select(t => new TaskStatsRow(
                    t.Id, t.Status, t.DueDate, t.CreatedAt, t.CompletedAt, t.ReturnCount, t.TargetType,
                    t.TargetType == AssignedTaskTargetType.Department ? t.DepartmentId ?? 0
                        : t.TargetType == AssignedTaskTargetType.Office ? t.OfficeId ?? 0 : t.AssigneeUserId ?? 0,
                    t.TargetType == AssignedTaskTargetType.Department ? (t.Department != null ? t.Department.Name : "—")
                        : t.TargetType == AssignedTaskTargetType.Office ? (t.Office != null ? t.Office.Name : "—")
                        : (t.AssigneeUser != null ? t.AssigneeUser.FullName : "—")))
                .ToListAsync();
    }
}
