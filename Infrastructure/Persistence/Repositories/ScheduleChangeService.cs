using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>حفظ التأخيرات وطلبات التمديد بأثرها في المعاملة نفسها (كـ ScheduleService)</summary>
    public class ScheduleChangeService : IScheduleChangeService
    {
        private readonly DataContext _context;

        public ScheduleChangeService(DataContext context) => _context = context;

        // ───────── التأخير ─────────

        public Task<List<ScheduleDelay>> GetDelaysAsync(int projectId, int take) =>
            _context.ScheduleDelays.AsNoTracking()
                .Include(d => d.Task).ThenInclude(t => t.Schedule)
                .Include(d => d.RecordedByUser).Include(d => d.DecidedByUser)
                .Include(d => d.ApprovalDocumentVersion).ThenInclude(v => v!.Document)
                .Include(d => d.AffectedTasks).ThenInclude(a => a.Task)
                .AsSplitQuery()
                .Where(d => d.ProjectId == projectId)
                .OrderByDescending(d => d.RecordedAt).ThenByDescending(d => d.Id)
                .Take(take).ToListAsync();

        public Task<ScheduleDelay?> GetDelayAsync(int id) =>
            _context.ScheduleDelays
                .Include(d => d.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User).ThenInclude(u => u.Department)
                .Include(d => d.Task).ThenInclude(t => t.Schedule)
                .Include(d => d.RecordedByUser).Include(d => d.DecidedByUser)
                .Include(d => d.ApprovalDocumentVersion).ThenInclude(v => v!.Document)
                .Include(d => d.AffectedTasks).ThenInclude(a => a.Task)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.Id == id);

        public Task<bool> HasLiveDelayAsync(int taskId) =>
            _context.ScheduleDelays.AnyAsync(d => d.TaskId == taskId && d.Status != DelayStatus.Resolved);

        public async Task AddDelayAsync(ScheduleDelay delay, AuditEntry audit, int branchId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.ScheduleDelays.Add(delay);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = delay.Id }, delay.ProjectId, branchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task SaveDelayAsync(ScheduleDelay delay, byte[]? rowVersion, AuditEntry audit, int branchId)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(delay).Property(d => d.RowVersion).OriginalValue = rowVersion;
            _context.AuditLogs.Add(NewLog(audit with { EntityId = delay.Id }, delay.ProjectId, branchId));
            await SaveAsync();
        }

        public Task<List<ScheduleDelay>> GetResolvableDelaysAsync(IReadOnlyCollection<int> taskIds) =>
            _context.ScheduleDelays
                .Where(d => taskIds.Contains(d.TaskId) && (d.Status == DelayStatus.Recorded || d.Status == DelayStatus.Approved))
                .ToListAsync();

        // ───────── التمديد ─────────

        public Task<List<ScheduleExtension>> GetExtensionsAsync(int projectId, int take) =>
            _context.ScheduleExtensions.AsNoTracking()
                .Include(x => x.RequestedByUser).Include(x => x.DecidedByUser)
                .Include(x => x.Items).ThenInclude(i => i.Task)
                .Include(x => x.Documents).ThenInclude(d => d.DocumentVersion).ThenInclude(v => v.Document)
                .AsSplitQuery()
                .Where(x => x.ProjectId == projectId)
                .OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id)
                .Take(take).ToListAsync();

        public Task<ScheduleExtension?> GetExtensionAsync(int id) =>
            _context.ScheduleExtensions
                .Include(x => x.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User).ThenInclude(u => u.Department)
                .Include(x => x.Schedule)
                .Include(x => x.RequestedByUser).Include(x => x.DecidedByUser)
                .Include(x => x.Items).ThenInclude(i => i.Task)
                .Include(x => x.Documents).ThenInclude(d => d.DocumentVersion).ThenInclude(v => v.Document)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id);

        public async Task AddExtensionAsync(ScheduleExtension extension, AuditEntry audit, int branchId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.ScheduleExtensions.Add(extension);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = extension.Id }, extension.ProjectId, branchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task SaveExtensionAsync(ScheduleExtension extension, byte[]? rowVersion, AuditEntry audit, int branchId)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(extension).Property(x => x.RowVersion).OriginalValue = rowVersion;
            _context.AuditLogs.Add(NewLog(audit with { EntityId = extension.Id }, extension.ProjectId, branchId));
            await SaveAsync();
        }

        public async Task DeleteExtensionAsync(ScheduleExtension extension, AuditEntry audit, int branchId)
        {
            _context.ScheduleExtensions.Remove(extension);
            _context.AuditLogs.Add(NewLog(audit, extension.ProjectId, branchId));
            await SaveAsync();
        }

        // ───────── تنبيه مرشَّحي التأخر ─────────

        public Task<List<ScheduleTask>> GetOverdueToNotifyAsync(DateTime today, int take) =>
            _context.ScheduleTasks
                .Include(t => t.Schedule).ThenInclude(s => s.Project)
                    .ThenInclude(p => p.Members).ThenInclude(m => m.User).ThenInclude(u => u.Department)
                .AsSplitQuery()
                .Where(t => t.Schedule.Status == ScheduleStatus.Approved
                            && t.Schedule.Project.Stage != ProjectStage.Closed && t.Schedule.Project.Stage != ProjectStage.Cancelled
                            && (t.Status == ScheduleTaskStatus.NotStarted || t.Status == ScheduleTaskStatus.InProgress)
                            && t.PlannedEnd < today
                            && (t.OverdueNotifiedFor == null || t.OverdueNotifiedFor != t.PlannedEnd))
                .OrderBy(t => t.PlannedEnd).ThenBy(t => t.Id)
                .Take(take).ToListAsync();

        public Task SaveChangesAsync() => SaveAsync();

        // ───────── مساعدات ─────────

        private static AuditLog NewLog(AuditEntry entry, int projectId, int branchId) => new()
        {
            CreatedAt = DateTime.UtcNow,
            UserId = entry.UserId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            ProjectId = projectId,
            BranchId = branchId,
            Title = Clip(entry.Title, 300),
            Details = Clip(entry.Details, 4000)
        };

        private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("تغيّر هذا السجل أو بنود الجدول المرتبطة به منذ فتحته — أعد فتح الصفحة وكرّر العملية");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                throw new InvalidOperationException("السجل موجود مسبقاً (عملية متزامنة) — أعد فتح الصفحة");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
            {
                throw new InvalidOperationException("العملية تخالف ارتباطاً أو قيداً في البيانات");
            }
        }
    }
}
