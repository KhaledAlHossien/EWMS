using Application.Features.Schedules;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// حفظ أمر المباشرة والجدول الزمني. كل عملية كتابة تحفظ أثرها في سجل التدقيق بمعاملتها نفسها،
    /// وما يحتاج حفظين (معرّف يُعرف بعد الإدراج، أو إحلال معتمد محل سابقه) يجري في معاملة واحدة.
    /// </summary>
    public class ScheduleService : IScheduleService
    {
        private readonly DataContext _context;

        public ScheduleService(DataContext context) => _context = context;

        // ───────── أمر المباشرة ─────────

        public Task<ProjectStartOrder?> GetStartOrderAsync(int projectId) =>
            _context.ProjectStartOrders
                .Include(o => o.SiteEngineer).Include(o => o.SectorSupervisor).Include(o => o.CreatedByUser)
                .FirstOrDefaultAsync(o => o.ProjectId == projectId);

        public async Task SaveStartOrderAsync(ProjectStartOrder order, bool isNew, byte[]? rowVersion, AuditEntry audit, int branchId)
        {
            order.UpdatedAt = DateTime.UtcNow;
            if (isNew)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                _context.ProjectStartOrders.Add(order);
                await SaveAsync();
                _context.AuditLogs.Add(NewLog(audit with { EntityId = order.Id }, order.ProjectId, branchId));
                await SaveAsync();
                await transaction.CommitAsync();
                return;
            }

            if (rowVersion is { Length: > 0 })
                _context.Entry(order).Property(o => o.RowVersion).OriginalValue = rowVersion;
            _context.AuditLogs.Add(NewLog(audit with { EntityId = order.Id }, order.ProjectId, branchId));
            await SaveAsync();
        }

        // ───────── الإصدارات ─────────

        public async Task<List<ScheduleVersionRow>> GetVersionsAsync(int projectId)
        {
            var schedules = await _context.ProjectSchedules.AsNoTracking()
                .Include(s => s.CreatedByUser).Include(s => s.ApprovedByUser)
                .Where(s => s.ProjectId == projectId)
                .OrderBy(s => s.VersionNumber)
                .ToListAsync();
            var ids = schedules.Select(s => s.Id).ToList();
            var tasks = (await _context.ScheduleTasks.AsNoTracking()
                    .Where(t => ids.Contains(t.ScheduleId))
                    .Select(t => new { t.ScheduleId, t.Progress, t.Weight })
                    .ToListAsync())
                .ToLookup(t => t.ScheduleId);

            // النسبة من ScheduleRules نفسها (مصدر واحد للمتوسط المرجَّح)
            return schedules.Select(s =>
            {
                var own = tasks[s.Id].Select(t => new ScheduleTask { Progress = t.Progress, Weight = t.Weight }).ToList();
                return new ScheduleVersionRow(s, own.Count, ScheduleRules.ActualProgress(own));
            }).ToList();
        }

        public Task<ProjectSchedule?> GetByIdAsync(int id) =>
            _context.ProjectSchedules
                .Include(s => s.CreatedByUser).Include(s => s.ApprovedByUser)
                .Include(s => s.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User).ThenInclude(u => u.Department)
                .Include(s => s.Tasks).ThenInclude(t => t.Assignee)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<ProjectSchedule?> GetApprovedAsync(int projectId)
        {
            var id = await _context.ProjectSchedules
                .Where(s => s.ProjectId == projectId && s.Status == ScheduleStatus.Approved)
                .Select(s => (int?)s.Id).FirstOrDefaultAsync();
            return id is int found ? await GetByIdAsync(found) : null;
        }

        public Task<List<ScheduleDependency>> GetDependenciesAsync(int scheduleId) =>
            _context.ScheduleDependencies.AsNoTracking().Where(d => d.ScheduleId == scheduleId).OrderBy(d => d.Id).ToListAsync();

        public Task<bool> HasPendingExtensionAsync(int projectId) =>
            _context.ScheduleExtensions.AnyAsync(x => x.ProjectId == projectId
                && (x.Status == ExtensionStatus.Draft || x.Status == ExtensionStatus.Submitted));

        public async Task<ProjectSchedule> CreateDraftAsync(ProjectSchedule draft, IReadOnlyList<(int OldId, ScheduleTask Copy)> copies,
            IReadOnlyList<ScheduleRules.Edge> edges, AuditEntry audit, int branchId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            draft.VersionNumber = (await _context.ProjectSchedules.Where(s => s.ProjectId == draft.ProjectId)
                .MaxAsync(s => (int?)s.VersionNumber) ?? 0) + 1;
            foreach (var (_, copy) in copies) draft.Tasks.Add(copy);
            _context.ProjectSchedules.Add(draft);
            await SaveAsync();

            // معرّفات البنود الجديدة تُعرف بعد الإدراج، فتُعاد التبعيات عليها
            var map = copies.ToDictionary(c => c.OldId, c => c.Copy.Id);
            foreach (var edge in edges)
                if (map.TryGetValue(edge.PredecessorId, out var predecessor) && map.TryGetValue(edge.SuccessorId, out var successor))
                    _context.ScheduleDependencies.Add(new ScheduleDependency { ScheduleId = draft.Id, PredecessorId = predecessor, SuccessorId = successor });

            _context.AuditLogs.Add(NewLog(audit with { EntityId = draft.Id }, draft.ProjectId, branchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return draft;
        }

        public async Task DeleteDraftAsync(ProjectSchedule draft, AuditEntry audit, int branchId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            // التبعية مفتاحها Restrict (مساران إلى البند): تُحذف قبل المسودة، والبنود تُحذف بالتسلسل
            await _context.ScheduleDependencies.Where(d => d.ScheduleId == draft.Id).ExecuteDeleteAsync();
            _context.ProjectSchedules.Remove(draft);
            _context.AuditLogs.Add(NewLog(audit, draft.ProjectId, branchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task ApproveAsync(ProjectSchedule draft, ProjectSchedule? previousApproved, int approverId, AuditEntry audit, int branchId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // الفهرس الفريد يسمح بمعتمد واحد: السابق يصير «سابقاً» أولاً ثم تُعتمد المسودة
            if (previousApproved != null)
            {
                previousApproved.Status = ScheduleStatus.Superseded;
                await SaveAsync();
            }

            ScheduleRules.SnapshotOriginals(draft.Tasks);
            draft.Status = ScheduleStatus.Approved;
            draft.ApprovedByUserId = approverId;
            draft.ApprovedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(NewLog(audit with { EntityId = draft.Id }, draft.ProjectId, branchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        // ───────── البنود والتبعيات ─────────

        public Task<ScheduleTask?> GetTaskAsync(int taskId) =>
            _context.ScheduleTasks
                .Include(t => t.Assignee)
                .Include(t => t.Schedule).ThenInclude(s => s.Project)
                    .ThenInclude(p => p.Members).ThenInclude(m => m.User).ThenInclude(u => u.Department)
                .AsSplitQuery()
                .FirstOrDefaultAsync(t => t.Id == taskId);

        public async Task AddTaskAsync(ScheduleTask task, AuditEntry audit, int branchId)
        {
            var projectId = await ProjectIdOfAsync(task.ScheduleId);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.ScheduleTasks.Add(task);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = task.Id }, projectId, branchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task UpdateTaskAsync(ScheduleTask task, byte[]? rowVersion, AuditEntry audit, int branchId)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(task).Property(t => t.RowVersion).OriginalValue = rowVersion;
            _context.AuditLogs.Add(NewLog(audit, await ProjectIdOfAsync(task.ScheduleId), branchId));
            await SaveAsync();
        }

        public async Task DeleteTaskAsync(ScheduleTask task, AuditEntry audit, int branchId)
        {
            var projectId = await ProjectIdOfAsync(task.ScheduleId);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.ScheduleDependencies.Where(d => d.PredecessorId == task.Id || d.SuccessorId == task.Id).ExecuteDeleteAsync();
            _context.ScheduleTasks.Remove(task);
            _context.AuditLogs.Add(NewLog(audit, projectId, branchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task ReorderAsync(int scheduleId, IReadOnlyList<int> orderedTaskIds, AuditEntry audit, int branchId)
        {
            var tasks = await _context.ScheduleTasks.Where(t => t.ScheduleId == scheduleId).ToListAsync();
            var position = orderedTaskIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index + 1);
            foreach (var task in tasks) task.SortOrder = position[task.Id];
            _context.AuditLogs.Add(NewLog(audit, await ProjectIdOfAsync(scheduleId), branchId));
            await SaveAsync();
        }

        public Task<ScheduleDependency?> GetDependencyAsync(int id) =>
            _context.ScheduleDependencies
                .Include(d => d.Predecessor).Include(d => d.Successor)
                .Include(d => d.Schedule).ThenInclude(s => s.Project)
                    .ThenInclude(p => p.Members).ThenInclude(m => m.User).ThenInclude(u => u.Department)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.Id == id);

        public async Task AddDependencyAsync(ScheduleDependency dependency, AuditEntry audit, int branchId)
        {
            var projectId = await ProjectIdOfAsync(dependency.ScheduleId);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.ScheduleDependencies.Add(dependency);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = dependency.Id }, projectId, branchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task RemoveDependencyAsync(ScheduleDependency dependency, AuditEntry audit, int branchId)
        {
            _context.ScheduleDependencies.Remove(dependency);
            _context.AuditLogs.Add(NewLog(audit, await ProjectIdOfAsync(dependency.ScheduleId), branchId));
            await SaveAsync();
        }

        // ───────── الإنجاز ─────────

        public async Task SaveProgressAsync(ScheduleTask task, ScheduleProgressEntry entry, byte[]? rowVersion, AuditEntry audit, int branchId)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(task).Property(t => t.RowVersion).OriginalValue = rowVersion;
            _context.ScheduleProgressEntries.Add(entry);
            _context.AuditLogs.Add(NewLog(audit, await ProjectIdOfAsync(task.ScheduleId), branchId));
            await SaveAsync();
        }

        public Task<List<ScheduleProgressEntry>> GetProgressHistoryAsync(int taskId, int take) =>
            _context.ScheduleProgressEntries.AsNoTracking().Include(e => e.User)
                .Where(e => e.TaskId == taskId)
                .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
                .Take(take).ToListAsync();

        // ───────── مساعدات ─────────

        private Task<int> ProjectIdOfAsync(int scheduleId) =>
            _context.ProjectSchedules.AsNoTracking().Where(s => s.Id == scheduleId).Select(s => s.ProjectId).FirstAsync();

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

        /// <summary>تعارض التزامن والفهارس الفريدة وقيود البيانات تتحول إلى رسالة واضحة بدل خطأ خادم</summary>
        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذا الجدول منذ فتحته — أعد فتحه وكرّر العملية");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                if (sql.Message.Contains("IX_ProjectSchedules_OneDraftOneApproved"))
                    throw new InvalidOperationException("يوجد إصدار معتمد أو مسودة بالفعل لهذا المشروع — أعد فتح الصفحة");
                if (sql.Message.Contains("IX_ProjectSchedules_ProjectId_VersionNumber"))
                    throw new InvalidOperationException("تعارض في رقم الإصدار بسبب عملية متزامنة — أعد المحاولة");
                if (sql.Message.Contains("IX_ScheduleDependencies_PredecessorId_SuccessorId"))
                    throw new InvalidOperationException("هذه التبعية مضافة مسبقاً");
                if (sql.Message.Contains("IX_ProjectStartOrders_ProjectId"))
                    throw new InvalidOperationException("لهذا المشروع أمر مباشرة مسجَّل بالفعل — أعد فتح الصفحة");
                throw;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
            {
                throw new InvalidOperationException("العملية تخالف ارتباطاً أو قيداً في البيانات (البند مرتبط بسجلات أخرى أو قيمته خارج المسموح)");
            }
        }
    }
}
