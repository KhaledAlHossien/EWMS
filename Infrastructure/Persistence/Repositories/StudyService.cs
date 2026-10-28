using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class StudyService : IStudyService
    {
        private readonly DataContext _context;

        public StudyService(DataContext context) => _context = context;

        // ───────── أنواع المخرجات ─────────

        public Task<List<DeliverableType>> GetTypesAsync(int? branchId, bool includeInactive) =>
            _context.DeliverableTypes.AsNoTracking()
                .Where(t => (branchId == null || t.BranchId == branchId) && (includeInactive || t.IsActive))
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();

        public Task<DeliverableType?> GetTypeAsync(int id) => _context.DeliverableTypes.FirstOrDefaultAsync(t => t.Id == id);

        public async Task<DeliverableType> AddTypeAsync(DeliverableType type, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.DeliverableTypes.Add(type);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = type.Id }, null, type.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return type;
        }

        public async Task UpdateTypeAsync(DeliverableType type, AuditEntry audit)
        {
            _context.AuditLogs.Add(NewLog(audit, null, type.BranchId));
            await SaveAsync();
        }

        public async Task DeleteTypeAsync(DeliverableType type, AuditEntry audit)
        {
            _context.DeliverableTypes.Remove(type);
            _context.AuditLogs.Add(NewLog(audit, null, type.BranchId));
            await SaveAsync();
        }

        public Task<bool> TypeNameExistsAsync(int branchId, string name, int? excludeId = null) =>
            _context.DeliverableTypes.AnyAsync(t => t.BranchId == branchId && t.Name == name && (excludeId == null || t.Id != excludeId));

        public Task<bool> TypeInUseAsync(int id) => _context.StudyDeliverables.AnyAsync(d => d.TypeId == id);

        // ───────── الأوامر ─────────

        private IQueryable<StudyOrder> Graph() => _context.StudyOrders
            .Include(o => o.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User)
            .Include(o => o.Correspondence)
            .Include(o => o.CreatedByUser)
            .Include(o => o.Deliverables).ThenInclude(d => d.Type)
            .Include(o => o.Deliverables).ThenInclude(d => d.Office).ThenInclude(of => of!.Department)
            .Include(o => o.Deliverables).ThenInclude(d => d.Assignee)
            .Include(o => o.Deliverables).ThenInclude(d => d.Document).ThenInclude(doc => doc!.Versions)
            .Include(o => o.Deliverables).ThenInclude(d => d.SubmittedByUser)
            .Include(o => o.Deliverables).ThenInclude(d => d.ReviewedByUser)
            .Include(o => o.Submissions).ThenInclude(s => s.SentByUser)
            .Include(o => o.Submissions).ThenInclude(s => s.DecidedByUser)
            .Include(o => o.Submissions).ThenInclude(s => s.OutgoingCorrespondence)
            .AsSplitQuery();

        public Task<StudyOrder?> GetByIdAsync(int id) => Graph().FirstOrDefaultAsync(o => o.Id == id);

        public Task<StudyOrder?> GetByDeliverableAsync(int deliverableId) =>
            Graph().FirstOrDefaultAsync(o => o.Deliverables.Any(d => d.Id == deliverableId));

        public Task<StudyOrder?> GetBySubmissionAsync(int submissionId) =>
            Graph().FirstOrDefaultAsync(o => o.Submissions.Any(s => s.Id == submissionId));

        public async Task<(List<StudyOrder> Items, int TotalCount)> SearchAsync(
            Expression<Func<Project, bool>> visibleProjects, StudyFilter f, int page, int pageSize)
        {
            var visibleIds = _context.Projects.Where(visibleProjects).Select(p => p.Id);
            var query = _context.StudyOrders.AsNoTracking().Where(o => visibleIds.Contains(o.ProjectId));

            if (f.ProjectId is int project) query = query.Where(o => o.ProjectId == project);
            if (f.BranchId is int branch) query = query.Where(o => o.Project.BranchId == branch);
            if (f.Status is StudyStatus status) query = query.Where(o => o.Status == status);
            if (f.AssigneeUserId is int assignee) query = query.Where(o => o.Deliverables.Any(d => d.AssigneeUserId == assignee));
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim();
                query = query.Where(o => o.Title.Contains(term) || o.Project.Name.Contains(term) || (o.ExternalNumber != null && o.ExternalNumber.Contains(term)));
            }
            if (f.OverdueOnly)
            {
                var today = f.Today.Date;
                query = query.Where(o => o.Status == StudyStatus.Open && o.Deliverables.Any(d => d.State != DeliverableState.Approved && d.DueDate != null && d.DueDate < today));
            }

            var total = await query.CountAsync();
            var items = await query.Include(o => o.Project).Include(o => o.Correspondence).Include(o => o.Deliverables)
                .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync();
            return (items, total);
        }

        public async Task<StudyOrder> AddOrderAsync(StudyOrder order, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            order.CreatedAt = now;
            order.UpdatedAt = now;
            _context.StudyOrders.Add(order);
            await SaveAsync();
            _context.AuditLogs.Add(await LogAsync(audit with { EntityId = order.Id }, order.ProjectId));
            await SaveAsync();
            await transaction.CommitAsync();
            return order;
        }

        public async Task UpdateOrderAsync(StudyOrder order, byte[]? rowVersion, AuditEntry audit)
        {
            Touch(order, rowVersion);
            _context.AuditLogs.Add(await LogAsync(audit, order.ProjectId));
            await SaveAsync();
        }

        // ───────── المخرجات ─────────

        public async Task AddDeliverableAsync(StudyOrder order, StudyDeliverable deliverable, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            deliverable.StudyOrderId = order.Id;
            _context.StudyDeliverables.Add(deliverable);
            order.UpdatedAt = DateTime.UtcNow;
            await SaveAsync();
            _context.AuditLogs.Add(await LogAsync(audit with { EntityId = deliverable.Id }, order.ProjectId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task RemoveDeliverableAsync(StudyOrder order, StudyDeliverable deliverable, AuditEntry audit)
        {
            _context.StudyDeliverables.Remove(deliverable);
            order.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(await LogAsync(audit, order.ProjectId));
            await SaveAsync();
        }

        public async Task UpdateDeliverableAsync(StudyOrder order, StudyDeliverable deliverable, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(deliverable).Property(d => d.RowVersion).OriginalValue = rowVersion;
            order.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(await LogAsync(audit, order.ProjectId));
            await SaveAsync();
        }

        public async Task ApproveDeliverableAsync(StudyOrder order, StudyDeliverable deliverable, int versionId, int reviewerId, byte[]? rowVersion, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;

            // الإصدار المعتمد في الإضبارة: لا يُعدَّل ولا يُحذف بعد اليوم (وإن اعتُمد من شاشة الوثائق قبلاً فلا شيء)
            await _context.ProjectDocumentVersions.Where(v => v.Id == versionId && !v.IsApproved)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsApproved, true)
                    .SetProperty(v => v.ApprovedByUserId, reviewerId).SetProperty(v => v.ApprovedAt, now));

            if (rowVersion is { Length: > 0 }) _context.Entry(deliverable).Property(d => d.RowVersion).OriginalValue = rowVersion;
            order.UpdatedAt = now;
            _context.AuditLogs.Add(await LogAsync(audit, order.ProjectId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        // ───────── الإرسال للمركزية ─────────

        public async Task AddSubmissionAsync(StudyOrder order, StudySubmission submission, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            submission.StudyOrderId = order.Id;
            _context.StudySubmissions.Add(submission);
            order.UpdatedAt = DateTime.UtcNow;
            await SaveAsync();
            _context.AuditLogs.Add(await LogAsync(audit with { EntityId = submission.Id }, order.ProjectId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task DecideSubmissionAsync(StudyOrder order, StudySubmission submission, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(submission).Property(s => s.RowVersion).OriginalValue = rowVersion;
            order.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(await LogAsync(audit with { EntityId = submission.Id }, order.ProjectId));
            await SaveAsync();
        }

        // ───────── التذكيرات ─────────

        public async Task<List<OverdueDeliverable>> GetOverdueCandidatesAsync(DateTime today, int take)
        {
            var day = today.Date;
            return await _context.StudyDeliverables.AsNoTracking()
                .Where(d => d.StudyOrder.Status == StudyStatus.Open && d.State != DeliverableState.Approved
                            && d.DueDate != null && d.DueDate < day && (d.OverdueNotifiedFor == null || d.OverdueNotifiedFor != d.DueDate))
                .OrderBy(d => d.DueDate).ThenBy(d => d.Id)
                .Select(d => new OverdueDeliverable(d.Id, d.StudyOrderId, d.StudyOrder.Title, d.Type.Name, d.StudyOrder.ProjectId,
                    d.StudyOrder.Project.BranchId, d.DueDate!.Value, d.AssigneeUserId, d.StudyOrder.CreatedByUserId))
                .Take(take).ToListAsync();
        }

        public Task MarkOverdueNotifiedAsync(int deliverableId, DateTime dueDate) =>
            _context.StudyDeliverables.Where(d => d.Id == deliverableId && d.DueDate == dueDate)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.OverdueNotifiedFor, dueDate));

        // ───────── الوثائق ─────────

        public Task<ProjectDocument?> GetDocumentAsync(int documentId) =>
            _context.ProjectDocuments.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == documentId);

        public Task<ProjectDocumentVersion?> GetVersionAsync(int versionId) =>
            _context.ProjectDocumentVersions.Include(v => v.Document).FirstOrDefaultAsync(v => v.Id == versionId);

        public Task<bool> DocumentInUseAsync(int documentId) => _context.StudyDeliverables.AnyAsync(d => d.DocumentId == documentId);

        // ───────── مساعدات ─────────

        private void Touch(StudyOrder order, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(order).Property(o => o.RowVersion).OriginalValue = rowVersion;
            order.UpdatedAt = DateTime.UtcNow;
        }

        private async Task<AuditLog> LogAsync(AuditEntry entry, int projectId)
        {
            var branchId = await _context.Projects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.BranchId).FirstAsync();
            return NewLog(entry, projectId, branchId);
        }

        private static AuditLog NewLog(AuditEntry entry, int? projectId, int branchId) => new()
        {
            CreatedAt = DateTime.UtcNow, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
            ProjectId = projectId, BranchId = branchId, Title = entry.Title, Details = entry.Details
        };

        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذه الدراسة منذ فتحتها — أعد فتحها وكرّر العملية");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                if (sql.Message.Contains("IX_DeliverableTypes_BranchId_Name"))
                    throw new InvalidOperationException("يوجد نوع مخرج بالاسم نفسه في هذا الفرع");
                if (sql.Message.Contains("IX_StudyDeliverables_StudyOrderId_TypeId"))
                    throw new InvalidOperationException("هذا النوع مضاف إلى الأمر مسبقاً");
                if (sql.Message.Contains("IX_StudySubmissions_OnePending") || sql.Message.Contains("IX_StudySubmissions_StudyOrderId_Round"))
                    throw new InvalidOperationException("بانتظار قرار الجولة السابقة، أو سُجّلت جولة بالتزامن");
                throw;
            }
        }
    }
}
