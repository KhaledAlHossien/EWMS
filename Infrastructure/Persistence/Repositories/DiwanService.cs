using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class DiwanService : IDiwanService
    {
        private const string CorrespondenceEntity = "Correspondence";

        private readonly DataContext _context;

        public DiwanService(DataContext context) => _context = context;

        // ───────── الأنواع والحالات ─────────

        public Task<List<CorrespondenceType>> GetTypesAsync(int? branchId, bool includeInactive) =>
            _context.CorrespondenceTypes.AsNoTracking()
                .Where(t => (branchId == null || t.BranchId == branchId) && (includeInactive || t.IsActive))
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();

        public Task<CorrespondenceType?> GetTypeAsync(int id) => _context.CorrespondenceTypes.FirstOrDefaultAsync(t => t.Id == id);

        public async Task<CorrespondenceType> AddTypeAsync(CorrespondenceType type, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.CorrespondenceTypes.Add(type);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = type.Id }, null, type.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return type;
        }

        public async Task UpdateTypeAsync(CorrespondenceType type, AuditEntry audit)
        {
            _context.AuditLogs.Add(NewLog(audit, null, type.BranchId));
            await SaveAsync();
        }

        public async Task DeleteTypeAsync(CorrespondenceType type, AuditEntry audit)
        {
            _context.CorrespondenceTypes.Remove(type);
            _context.AuditLogs.Add(NewLog(audit, null, type.BranchId));
            await SaveAsync();
        }

        public Task<bool> TypeNameExistsAsync(int branchId, string name, int? excludeId = null) =>
            _context.CorrespondenceTypes.AnyAsync(t => t.BranchId == branchId && t.Name == name && (excludeId == null || t.Id != excludeId));

        public Task<bool> TypeInUseAsync(int id) => _context.Correspondences.AnyAsync(c => c.TypeId == id);

        public Task<List<CorrespondenceStatus>> GetStatusesAsync(int? branchId, bool includeInactive) =>
            _context.CorrespondenceStatuses.AsNoTracking()
                .Where(s => (branchId == null || s.BranchId == branchId) && (includeInactive || s.IsActive))
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync();

        public Task<CorrespondenceStatus?> GetStatusAsync(int id) => _context.CorrespondenceStatuses.FirstOrDefaultAsync(s => s.Id == id);

        public async Task<CorrespondenceStatus> AddStatusAsync(CorrespondenceStatus status, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.CorrespondenceStatuses.Add(status);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = status.Id }, null, status.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return status;
        }

        public async Task UpdateStatusAsync(CorrespondenceStatus status, AuditEntry audit)
        {
            _context.AuditLogs.Add(NewLog(audit, null, status.BranchId));
            await SaveAsync();
        }

        public async Task DeleteStatusAsync(CorrespondenceStatus status, AuditEntry audit)
        {
            _context.CorrespondenceStatuses.Remove(status);
            _context.AuditLogs.Add(NewLog(audit, null, status.BranchId));
            await SaveAsync();
        }

        public Task<bool> StatusNameExistsAsync(int branchId, string name, int? excludeId = null) =>
            _context.CorrespondenceStatuses.AnyAsync(s => s.BranchId == branchId && s.Name == name && (excludeId == null || s.Id != excludeId));

        public Task<bool> StatusInUseAsync(int id) => _context.Correspondences.AnyAsync(c => c.StatusId == id);

        // ───────── المعاملات ─────────

        private IQueryable<Correspondence> WithDetails(IQueryable<Correspondence> query) => query
            .Include(c => c.Branch).Include(c => c.Department).Include(c => c.Type).Include(c => c.Status).Include(c => c.Project)
            .Include(c => c.CurrentDepartment).Include(c => c.CurrentOffice).Include(c => c.CurrentUser)
            .Include(c => c.ReplyTo).Include(c => c.RegisteredByUser).Include(c => c.ApprovedByUser).Include(c => c.IssuedByUser)
            .Include(c => c.Referrals).ThenInclude(r => r.FromUser)
            .Include(c => c.Referrals).ThenInclude(r => r.FromDepartment)
            .Include(c => c.Referrals).ThenInclude(r => r.ToDepartment)
            .Include(c => c.Referrals).ThenInclude(r => r.ToOffice)
            .Include(c => c.Referrals).ThenInclude(r => r.ToUser)
            .Include(c => c.Referrals).ThenInclude(r => r.AcknowledgedByUser)
            .Include(c => c.Attachments).ThenInclude(a => a.UploadedByUser)
            .AsSplitQuery();

        public Task<Correspondence?> GetByIdAsync(int id) => WithDetails(_context.Correspondences).FirstOrDefaultAsync(c => c.Id == id);

        public async Task<(List<Correspondence> Items, int TotalCount)> SearchAsync(
            Expression<Func<Correspondence, bool>> visible, DiwanFilter filter, int page, int pageSize)
        {
            var query = _context.Correspondences.AsNoTracking().Where(visible);
            if (filter.Inbox is { } inbox) query = query.Where(inbox);
            if (filter.Direction is CorrespondenceDirection direction) query = query.Where(c => c.Direction == direction);
            if (filter.BranchId is int branch) query = query.Where(c => c.BranchId == branch);
            if (filter.TypeId is int type) query = query.Where(c => c.TypeId == type);
            if (filter.StatusId is int status) query = query.Where(c => c.StatusId == status);
            if (filter.Stage is CorrespondenceStage stage) query = query.Where(c => c.Status != null && c.Status.Stage == stage);
            if (filter.ProjectId is int project) query = query.Where(c => c.ProjectId == project);
            if (filter.From is DateTime from) query = query.Where(c => c.RegisteredAt >= from);
            if (filter.To is DateTime to) query = query.Where(c => c.RegisteredAt < to.Date.AddDays(1));
            if (filter.OverdueOnly)
            {
                var today = filter.Today.Date;
                query = query.Where(c => c.Direction == CorrespondenceDirection.Incoming && c.DueDate != null && c.DueDate < today
                                         && c.Status != null && (c.Status.Stage == CorrespondenceStage.Registered || c.Status.Stage == CorrespondenceStage.InProgress));
            }
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                // «IN-2026-00012» أو «12»: الأرقام الأخيرة = التسلسل
                var digits = new string(text.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                var sequence = (text.StartsWith("IN", StringComparison.OrdinalIgnoreCase) || text.StartsWith("OUT", StringComparison.OrdinalIgnoreCase))
                               && int.TryParse(digits, out var n) ? n : -1;
                query = query.Where(c => c.Sequence == sequence || c.Subject.Contains(text) || c.Party.Contains(text)
                                         || (c.ExternalNumber != null && c.ExternalNumber.Contains(text)));
            }

            var total = await query.CountAsync();
            var items = await query
                .Include(c => c.Type).Include(c => c.Status).Include(c => c.Project)
                .Include(c => c.CurrentDepartment).Include(c => c.CurrentOffice).Include(c => c.RegisteredByUser)
                .OrderByDescending(c => c.RegisteredAt).ThenByDescending(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public async Task<Correspondence> AddAsync(Correspondence correspondence, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            if (correspondence.Direction == CorrespondenceDirection.Incoming)
                correspondence.Sequence = await NextSequenceAsync(correspondence.BranchId, correspondence.Direction, correspondence.Year);

            _context.Correspondences.Add(correspondence);
            await SaveAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = correspondence.Id }, correspondence.ProjectId, correspondence.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return correspondence;
        }

        public async Task UpdateAsync(Correspondence correspondence, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(correspondence).Property(c => c.RowVersion).OriginalValue = rowVersion;
            correspondence.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(NewLog(audit, correspondence.ProjectId, correspondence.BranchId));
            await SaveAsync();
        }

        public async Task AddReferralAsync(Correspondence correspondence, CorrespondenceReferral referral, AuditEntry audit)
        {
            correspondence.UpdatedAt = DateTime.UtcNow;
            _context.CorrespondenceReferrals.Add(referral);
            _context.AuditLogs.Add(NewLog(audit, correspondence.ProjectId, correspondence.BranchId));
            await SaveAsync();
        }

        public async Task AcknowledgeAsync(Correspondence correspondence, CorrespondenceReferral referral, int userId, AuditEntry audit)
        {
            referral.AcknowledgedAt = DateTime.UtcNow;
            referral.AcknowledgedByUserId = userId;
            _context.AuditLogs.Add(NewLog(audit, correspondence.ProjectId, correspondence.BranchId));
            await SaveAsync();
        }

        public async Task IssueAsync(Correspondence correspondence, int userId, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            correspondence.Year = DateTime.Today.Year;
            correspondence.Sequence = await NextSequenceAsync(correspondence.BranchId, correspondence.Direction, correspondence.Year);
            correspondence.OutgoingState = OutgoingState.Issued;
            correspondence.IssuedAt = now;
            correspondence.IssuedByUserId = userId;
            correspondence.UpdatedAt = now;
            await SaveAsync();
            // رقمه لا يُعرف قبل الحفظ: يدخل في عنوان الأثر
            _context.AuditLogs.Add(NewLog(audit, correspondence.ProjectId, correspondence.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
        }

        public Task<CorrespondenceReferral?> GetReferralAsync(int id) => _context.CorrespondenceReferrals.FirstOrDefaultAsync(r => r.Id == id);

        /// <summary>
        /// التسلسل التالي داخل (الفرع، الاتجاه، السنة): قفل تطبيقي على المفتاح داخل المعاملة الجارية يمنع رقمين متطابقين
        /// عند تسجيلين متزامنين (والفهرس الفريد خط الدفاع الأخير). يُحرَّر القفل مع نهاية المعاملة.
        /// </summary>
        private async Task<int> NextSequenceAsync(int branchId, CorrespondenceDirection direction, int year)
        {
            await _context.Database.ExecuteSqlRawAsync(@"
DECLARE @r int;
EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @r < 0 THROW 50003, N'diwan numbering lock timeout', 1;", $"diwan-{branchId}-{(int)direction}-{year}");

            var last = await _context.Correspondences
                .Where(c => c.BranchId == branchId && c.Direction == direction && c.Year == year)
                .MaxAsync(c => (int?)c.Sequence);
            return (last ?? 0) + 1;
        }

        // ───────── المرفقات ─────────

        public async Task AddAttachmentsAsync(Correspondence correspondence, IReadOnlyCollection<CorrespondenceAttachment> attachments, AuditEntry audit)
        {
            _context.CorrespondenceAttachments.AddRange(attachments);
            correspondence.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(NewLog(audit, correspondence.ProjectId, correspondence.BranchId));
            await SaveAsync();
        }

        public Task<CorrespondenceAttachment?> GetAttachmentAsync(int id) =>
            _context.CorrespondenceAttachments.FirstOrDefaultAsync(a => a.Id == id);

        public async Task<byte[]?> GetAttachmentDataAsync(int id) =>
            (await _context.CorrespondenceAttachmentContents.AsNoTracking().FirstOrDefaultAsync(c => c.AttachmentId == id))?.Data;

        public async Task DeleteAttachmentAsync(Correspondence correspondence, CorrespondenceAttachment attachment, AuditEntry audit)
        {
            _context.CorrespondenceAttachments.Remove(attachment);
            correspondence.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(NewLog(audit, correspondence.ProjectId, correspondence.BranchId));
            await SaveAsync();
        }

        // ───────── التذكيرات ─────────

        public Task<List<Correspondence>> GetReminderCandidatesAsync(DateTime today, int take)
        {
            var day = today.Date;
            var tomorrow = day.AddDays(1);
            return _context.Correspondences.AsNoTracking()
                .Include(c => c.Status).Include(c => c.Type).Include(c => c.RegisteredByUser)
                .Where(c => c.Direction == CorrespondenceDirection.Incoming && c.DueDate != null && c.DueDate <= tomorrow
                            && c.Status != null && (c.Status.Stage == CorrespondenceStage.Registered || c.Status.Stage == CorrespondenceStage.InProgress)
                            && ((c.DueDate < day && c.OverdueNotifiedAt == null) || (c.DueDate >= day && c.DueSoonNotifiedAt == null)))
                .OrderBy(c => c.DueDate).ThenBy(c => c.Id)
                .Take(take)
                .ToListAsync();
        }

        public async Task MarkReminderSentAsync(int id, bool overdue)
        {
            var now = DateTime.UtcNow;
            var rows = _context.Correspondences.Where(c => c.Id == id);
            if (overdue) await rows.ExecuteUpdateAsync(s => s.SetProperty(c => c.OverdueNotifiedAt, now));
            else await rows.ExecuteUpdateAsync(s => s.SetProperty(c => c.DueSoonNotifiedAt, now));
        }

        // ───────── السجل ─────────

        public Task<List<AuditLog>> GetHistoryAsync(int correspondenceId) =>
            _context.AuditLogs.AsNoTracking().Include(a => a.User)
                .Where(a => a.EntityType == CorrespondenceEntity && a.EntityId == correspondenceId)
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).ToListAsync();

        // ───────── حراس الحذف ─────────

        public async Task<bool> ExistsForBranchAsync(int branchId) =>
            await _context.Correspondences.AnyAsync(c => c.BranchId == branchId)
            || await _context.CorrespondenceTypes.AnyAsync(t => t.BranchId == branchId)
            || await _context.CorrespondenceStatuses.AnyAsync(s => s.BranchId == branchId);

        public async Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            await _context.Correspondences.AnyAsync(c => c.DepartmentId == departmentId || c.CurrentDepartmentId == departmentId)
            || await _context.CorrespondenceReferrals.AnyAsync(r => r.ToDepartmentId == departmentId || r.FromDepartmentId == departmentId);

        public async Task<bool> ExistsForOfficeAsync(int officeId) =>
            await _context.Correspondences.AnyAsync(c => c.CurrentOfficeId == officeId)
            || await _context.CorrespondenceReferrals.AnyAsync(r => r.ToOfficeId == officeId);

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.Correspondences.AnyAsync(c => c.RegisteredByUserId == userId || c.CurrentUserId == userId || c.ApprovedByUserId == userId
                                                          || c.IssuedByUserId == userId || c.ClosedByUserId == userId)
            || await _context.CorrespondenceReferrals.AnyAsync(r => r.FromUserId == userId || r.ToUserId == userId || r.AcknowledgedByUserId == userId)
            || await _context.CorrespondenceAttachments.AnyAsync(a => a.UploadedByUserId == userId);

        public Task<bool> ExistsForProjectAsync(int projectId) => _context.Correspondences.AnyAsync(c => c.ProjectId == projectId);

        // ───────── مساعدات ─────────

        private static AuditLog NewLog(AuditEntry entry, int? projectId, int branchId) => new()
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
                throw new InvalidOperationException("عدّل مستخدم آخر هذه المعاملة منذ فتحتها — أعد فتحها وكرّر العملية");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                if (sql.Message.Contains("IX_CorrespondenceTypes_BranchId_Name"))
                    throw new InvalidOperationException("يوجد نوع بالاسم نفسه في هذا الفرع");
                if (sql.Message.Contains("IX_CorrespondenceStatuses_BranchId_Name"))
                    throw new InvalidOperationException("توجد حالة بالاسم نفسه في هذا الفرع");
                if (sql.Message.Contains("IX_Correspondences_BranchId_Direction_Year_Sequence"))
                    throw new InvalidOperationException("تعذّر ترقيم المعاملة لتزامن تسجيلين، أعد المحاولة");
                throw;
            }
        }
    }
}
