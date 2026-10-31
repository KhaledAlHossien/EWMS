using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class HandoverService : IHandoverService
    {
        private readonly DataContext _context;

        public HandoverService(DataContext context) => _context = context;

        private IQueryable<HandoverRecord> Graph() => _context.HandoverRecords
            .Include(h => h.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User)
            .Include(h => h.Contract)
            .Include(h => h.Correspondence)
            .Include(h => h.RequestedByUser)
            .Include(h => h.ConcludedByUser)
            .Include(h => h.Committee).ThenInclude(c => c!.Members).ThenInclude(m => m.User)
            .Include(h => h.Notes).ThenInclude(n => n.ResponsibleUser)
            .Include(h => h.Notes).ThenInclude(n => n.FixedByUser)
            .Include(h => h.Notes).ThenInclude(n => n.VerifiedByUser)
            .AsSplitQuery();

        public Task<HandoverRecord?> GetByIdAsync(int id) => Graph().FirstOrDefaultAsync(h => h.Id == id);

        public Task<HandoverRecord?> GetByNoteAsync(int noteId) => Graph().FirstOrDefaultAsync(h => h.Notes.Any(n => n.Id == noteId));

        public Task<List<HandoverRecord>> GetByProjectAsync(int projectId) =>
            _context.HandoverRecords.Include(h => h.Notes).Where(h => h.ProjectId == projectId).OrderBy(h => h.Id).ToListAsync();

        public async Task<(List<HandoverRecord> Items, int TotalCount)> SearchAsync(Expression<Func<Project, bool>> visibleProjects, HandoverFilter f, int page, int pageSize)
        {
            var visibleIds = _context.Projects.Where(visibleProjects).Select(p => p.Id);
            var query = _context.HandoverRecords.AsNoTracking().Where(h => visibleIds.Contains(h.ProjectId));

            if (f.ProjectId is int project) query = query.Where(h => h.ProjectId == project);
            if (f.BranchId is int branch) query = query.Where(h => h.Project.BranchId == branch);
            if (f.Kind is HandoverKind kind) query = query.Where(h => h.Kind == kind);
            if (f.Status is HandoverStatus status) query = query.Where(h => h.Status == status);
            if (f.OpenNotesOnly) query = query.Where(h => h.Notes.Any(n => n.Status != HandoverNoteStatus.Verified));
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var term = f.Search.Trim();
                query = query.Where(h => h.Project.Name.Contains(term) || (h.Contract != null && h.Contract.Number != null && h.Contract.Number.Contains(term)));
            }

            var total = await query.CountAsync();
            var items = await query.Include(h => h.Project).Include(h => h.Contract).Include(h => h.Notes)
                .OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync();
            return (items, total);
        }

        public Task<int> CountOpenNotesElsewhereAsync(int projectId, int excludeRecordId) =>
            _context.HandoverNotes.CountAsync(n => n.HandoverRecord.ProjectId == projectId && n.HandoverRecordId != excludeRecordId
                && n.HandoverRecord.Status != HandoverStatus.Cancelled && n.Status != HandoverNoteStatus.Verified);

        public async Task<ProjectClosureData> GetClosureDataAsync(int projectId)
        {
            var records = await _context.HandoverRecords.AsNoTracking().Include(h => h.Notes).Where(h => h.ProjectId == projectId).ToListAsync();
            var contracts = await _context.Contracts.AsNoTracking().Include(c => c.RetentionEntries).ThenInclude(r => r.Decisions)
                .Where(c => c.ProjectId == projectId).AsSplitQuery().ToListAsync();
            return new ProjectClosureData(records, contracts);
        }

        public async Task<HandoverRecord> AddAsync(HandoverRecord record, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            record.CreatedAt = now;
            record.UpdatedAt = now;
            _context.HandoverRecords.Add(record);
            await SaveAsync();
            _context.AuditLogs.Add(await LogAsync(audit with { EntityId = record.Id }, record.ProjectId));
            await SaveAsync();
            await transaction.CommitAsync();
            return record;
        }

        public async Task UpdateAsync(HandoverRecord record, byte[]? rowVersion, AuditEntry audit)
        {
            Touch(record, rowVersion);
            _context.AuditLogs.Add(await LogAsync(audit, record.ProjectId));
            await SaveAsync();
        }

        public async Task SaveCommitteeAsync(HandoverRecord record, Committee committee, bool isNew, IReadOnlyCollection<CommitteeMember> added,
            IReadOnlyCollection<CommitteeMember> removed, byte[]? rowVersion, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            if (isNew)
            {
                Touch(record, rowVersion);
                committee.HandoverRecordId = record.Id;
                _context.Committees.Add(committee);
            }
            else
            {
                Touch(record, null);
                if (rowVersion is { Length: > 0 }) _context.Entry(committee).Property(c => c.RowVersion).OriginalValue = rowVersion;
                _context.CommitteeMembers.RemoveRange(removed);
            }
            await SaveAsync();

            // الأعضاء الجدد بعد حذف القدامى (رئيس واحد في الفهرس الفريد)
            if (!isNew && added.Count > 0)
            {
                foreach (var member in added) member.CommitteeId = committee.Id;
                _context.CommitteeMembers.AddRange(added);
            }
            _context.AuditLogs.Add(await LogAsync(audit, record.ProjectId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public async Task AddNoteAsync(HandoverRecord record, HandoverNote note, AuditEntry audit)
        {
            note.HandoverRecordId = record.Id;
            _context.HandoverNotes.Add(note);
            Touch(record, null);
            _context.AuditLogs.Add(await LogAsync(audit, record.ProjectId));
            await SaveAsync();
        }

        public async Task SaveNoteAsync(HandoverRecord record, HandoverNote note, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(note).Property(n => n.RowVersion).OriginalValue = rowVersion;
            Touch(record, null);
            _context.AuditLogs.Add(await LogAsync(audit, record.ProjectId));
            await SaveAsync();
        }

        public async Task RemoveNoteAsync(HandoverRecord record, HandoverNote note, AuditEntry audit)
        {
            _context.HandoverNotes.Remove(note);
            Touch(record, null);
            _context.AuditLogs.Add(await LogAsync(audit, record.ProjectId));
            await SaveAsync();
        }

        // ───────── التذكيرات ─────────

        public Task<List<HandoverNote>> GetOverdueNotesAsync(DateTime today, int take)
        {
            var day = today.Date;
            return _context.HandoverNotes.AsNoTracking()
                .Include(n => n.HandoverRecord).ThenInclude(h => h.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User)
                .Where(n => n.Status != HandoverNoteStatus.Verified && n.DueDate != null && n.DueDate < day
                            && (n.OverdueNotifiedFor == null || n.OverdueNotifiedFor != n.DueDate)
                            && n.HandoverRecord.Status != HandoverStatus.Cancelled)
                .OrderBy(n => n.DueDate).ThenBy(n => n.Id).Take(take).AsSplitQuery().ToListAsync();
        }

        public Task MarkNoteOverdueNotifiedAsync(int noteId, DateTime dueDate) =>
            _context.HandoverNotes.Where(n => n.Id == noteId && n.DueDate == dueDate)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.OverdueNotifiedFor, dueDate));

        // ───────── الوثائق ─────────

        public Task<ProjectDocumentVersion?> GetVersionAsync(int versionId) =>
            _context.ProjectDocumentVersions.Include(v => v.Document).FirstOrDefaultAsync(v => v.Id == versionId);

        // ───────── مساعدات ─────────

        private void Touch(HandoverRecord record, byte[]? rowVersion)
        {
            if (rowVersion is { Length: > 0 }) _context.Entry(record).Property(h => h.RowVersion).OriginalValue = rowVersion;
            record.UpdatedAt = DateTime.UtcNow;
        }

        private async Task<AuditLog> LogAsync(AuditEntry entry, int projectId)
        {
            var branchId = await _context.Projects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.BranchId).FirstAsync();
            return new AuditLog
            {
                CreatedAt = DateTime.UtcNow, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
                ProjectId = projectId, BranchId = branchId, Title = entry.Title, Details = entry.Details
            };
        }

        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذا المحضر منذ فتحته — أعد فتحه وكرّر العملية");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                if (sql.Message.Contains("IX_HandoverRecords_OneOpenPerKind")) throw new InvalidOperationException("لهذا النوع طلب استلام لم ينتهِ بعد");
                if (sql.Message.Contains("IX_HandoverRecords_OneFinalConcluded")) throw new InvalidOperationException("سُجّل استلام نهائي بلا ملاحظات لهذا المشروع");
                if (sql.Message.Contains("IX_Committees_OnePerHandover")) throw new InvalidOperationException("شُكّلت لجنة لهذا الطلب مسبقاً");
                if (sql.Message.Contains("IX_CommitteeMembers_OneChair")) throw new InvalidOperationException("للجنة رئيس واحد فقط");
                if (sql.Message.Contains("IX_CommitteeMembers_OnePerUser")) throw new InvalidOperationException("موظف مكرر في أعضاء اللجنة");
                throw;
            }
        }
    }
}
