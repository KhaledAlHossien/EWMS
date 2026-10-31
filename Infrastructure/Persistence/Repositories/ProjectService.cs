using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ProjectService : IProjectService
    {
        private readonly DataContext _context;

        public ProjectService(DataContext context) => _context = context;

        public Task<Project?> GetByIdAsync(int id) =>
            _context.Projects
                .Include(p => p.Branch).Include(p => p.Department).Include(p => p.Site).Include(p => p.CreatedByUser)
                .Include(p => p.Members).ThenInclude(m => m.User).ThenInclude(u => u.Department)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<(List<Project> Items, int TotalCount)> SearchAsync(
            Expression<Func<Project, bool>> visible, ProjectFilter filter, int page, int pageSize)
        {
            var query = _context.Projects.AsNoTracking().Where(visible);
            if (filter.BranchId is int branch) query = query.Where(p => p.BranchId == branch);
            if (filter.DepartmentId is int department) query = query.Where(p => p.DepartmentId == department);
            if (filter.SiteId is int site) query = query.Where(p => p.SiteId == site);
            if (filter.Stage is ProjectStage stage) query = query.Where(p => p.Stage == stage);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                // «PRJ-2026-00042» أو «42»: الأرقام الأخيرة = رقم المشروع
                var digits = new string(text.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                var id = text.StartsWith("PRJ", StringComparison.OrdinalIgnoreCase) && int.TryParse(digits, out var n) ? n : -1;
                query = query.Where(p => p.Id == id || p.Name.Contains(text) || p.Beneficiary.Contains(text)
                                         || (p.ReferenceNumber != null && p.ReferenceNumber.Contains(text)));
            }

            var total = await query.CountAsync();
            var items = await query
                .Include(p => p.Branch).Include(p => p.Department).Include(p => p.Site).Include(p => p.CreatedByUser)
                .Include(p => p.Members)
                .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public async Task<Project> AddAsync(Project project, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.Projects.Add(project);
            await SaveAsync();

            // رقم المشروع لا يُعرف قبل الحفظ: يُكمَّل الأثر به ويُحفظ في المعاملة نفسها
            _context.AuditLogs.Add(NewLog(audit with { EntityId = project.Id }, project.Id, project.BranchId));
            await SaveAsync();
            await transaction.CommitAsync();
            return project;
        }

        public async Task UpdateAsync(Project project, byte[]? rowVersion, AuditEntry audit)
        {
            if (rowVersion is { Length: > 0 })
                _context.Entry(project).Property(p => p.RowVersion).OriginalValue = rowVersion;
            project.UpdatedAt = DateTime.UtcNow;
            _context.AuditLogs.Add(NewLog(audit, project.Id, project.BranchId));
            await SaveAsync();
        }

        public async Task AddMemberAsync(ProjectMember member, AuditEntry audit)
        {
            var project = member.Project ?? await _context.Projects.FirstAsync(p => p.Id == member.ProjectId);
            _context.ProjectMembers.Add(member);
            _context.AuditLogs.Add(NewLog(audit, project.Id, project.BranchId));
            await SaveAsync();
        }

        public async Task RemoveMemberAsync(ProjectMember member, AuditEntry audit)
        {
            var project = await _context.Projects.AsNoTracking().FirstAsync(p => p.Id == member.ProjectId);
            _context.ProjectMembers.Remove(member);
            _context.AuditLogs.Add(NewLog(audit, project.Id, project.BranchId));
            await SaveAsync();
        }

        public Task<bool> ReferenceExistsAsync(int branchId, string reference, int? excludeProjectId = null) =>
            _context.Projects.AnyAsync(p => p.BranchId == branchId && p.ReferenceNumber == reference
                                            && (excludeProjectId == null || p.Id != excludeProjectId));

        public Task<List<User>> GetMemberCandidatesAsync(int branchId, string? search, IReadOnlyCollection<int> excludeUserIds, int take)
        {
            var query = _context.Users.AsNoTracking().Include(u => u.Department)
                .Where(u => u.IsActive && u.BranchId == branchId && !excludeUserIds.Contains(u.Id));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var text = search.Trim();
                query = query.Where(u => u.FullName.Contains(text));
            }
            return query.OrderBy(u => u.FullName).Take(take).ToListAsync();
        }

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

        /// <summary>تعارض التزامن وتكرار الرقم المرجعي (فهرس فريد) يتحوّلان إلى رسالة واضحة بدل خطأ خادم</summary>
        private async Task SaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذا المشروع منذ فتحته — أعد فتحه وكرّر التعديل");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                if (sql.Message.Contains("IX_Projects_BranchId_ReferenceNumber"))
                    throw new InvalidOperationException("يوجد مشروع بالرقم المرجعي نفسه في هذا الفرع");
                if (sql.Message.Contains("PK_ProjectMembers"))
                    throw new InvalidOperationException("هذا الموظف مشارك في المشروع مسبقاً");
                throw;
            }
        }

        public async Task<bool> ExistsForBranchAsync(int branchId) =>
            await _context.Projects.AnyAsync(p => p.BranchId == branchId)
            || await _context.DeliverableTypes.AnyAsync(t => t.BranchId == branchId)
            || await _context.Contractors.AnyAsync(c => c.BranchId == branchId)
            || await _context.EvaluationCriteria.AnyAsync(c => c.BranchId == branchId);

        // مشاركة موظفي القسم لا تمنع حذفه (المنع بحارس «له مستخدمون» أصلاً)؛ المانع هنا مشروع يعالجه القسم
        public Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            _context.Projects.AnyAsync(p => p.DepartmentId == departmentId);

        public Task<bool> ExistsForOfficeAsync(int officeId) =>
            _context.StudyDeliverables.AnyAsync(d => d.OfficeId == officeId);

        public Task<bool> ExistsForSiteAsync(int siteId) =>
            _context.Projects.AnyAsync(p => p.SiteId == siteId);

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.Projects.AnyAsync(p => p.CreatedByUserId == userId)
            || await _context.ProjectMembers.AnyAsync(m => m.UserId == userId)
            || await _context.ProjectDocuments.AnyAsync(d => d.CreatedByUserId == userId)
            || await _context.ProjectDocumentVersions.AnyAsync(v => v.UploadedByUserId == userId || v.ApprovedByUserId == userId)
            || await _context.ProjectUploads.AnyAsync(u => u.UserId == userId)
            || await _context.ProjectStartOrders.AnyAsync(o => o.CreatedByUserId == userId || o.SiteEngineerUserId == userId || o.SectorSupervisorUserId == userId)
            || await _context.ProjectSchedules.AnyAsync(s => s.CreatedByUserId == userId || s.ApprovedByUserId == userId)
            || await _context.ScheduleTasks.AnyAsync(t => t.AssigneeUserId == userId)
            || await _context.ScheduleProgressEntries.AnyAsync(e => e.UserId == userId)
            || await _context.ScheduleDelays.AnyAsync(d => d.RecordedByUserId == userId || d.DecidedByUserId == userId)
            || await _context.ScheduleExtensions.AnyAsync(x => x.RequestedByUserId == userId || x.DecidedByUserId == userId)
            || await _context.StudyOrders.AnyAsync(o => o.CreatedByUserId == userId)
            || await _context.StudyDeliverables.AnyAsync(d => d.AssigneeUserId == userId || d.SubmittedByUserId == userId || d.ReviewedByUserId == userId)
            || await _context.StudySubmissions.AnyAsync(s => s.SentByUserId == userId || s.DecidedByUserId == userId)
            || await _context.Contracts.AnyAsync(c => c.CreatedByUserId == userId)
            || await _context.ContractOffers.AnyAsync(o => o.RecordedByUserId == userId)
            || await _context.ContractAmendments.AnyAsync(a => a.RecordedByUserId == userId)
            || await _context.Committees.AnyAsync(c => c.CreatedByUserId == userId)
            || await _context.CommitteeMembers.AnyAsync(m => m.UserId == userId)
            || await _context.RetentionEntries.AnyAsync(r => r.RecordedByUserId == userId)
            || await _context.RetentionDecisions.AnyAsync(d => d.RecordedByUserId == userId)
            || await _context.ContractorEvaluations.AnyAsync(e => e.EvaluatedByUserId == userId || e.ReviewedByUserId == userId)
            || await _context.HandoverRecords.AnyAsync(h => h.RequestedByUserId == userId || h.ConcludedByUserId == userId)
            || await _context.HandoverNotes.AnyAsync(n => n.ResponsibleUserId == userId || n.FixedByUserId == userId || n.VerifiedByUserId == userId)
            || await _context.AuditLogs.AnyAsync(a => a.UserId == userId);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly DataContext _context;

        public AuditLogService(DataContext context) => _context = context;

        public async Task WriteAsync(AuditEntry entry, int projectId, int branchId)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                CreatedAt = DateTime.UtcNow, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
                ProjectId = projectId, BranchId = branchId, Title = entry.Title, Details = entry.Details
            });
            await _context.SaveChangesAsync();
        }

        public async Task<(List<AuditLog> Items, int TotalCount)> GetForProjectAsync(
            int projectId, string? entityType, int? userId, int page, int pageSize)
        {
            var query = _context.AuditLogs.AsNoTracking().Where(a => a.ProjectId == projectId);
            return await PageAsync(query, entityType, userId, null, null, page, pageSize);
        }

        public async Task<(List<AuditLog> Items, int TotalCount)> SearchAsync(
            Expression<Func<Project, bool>> visibleProjects, bool includeWithoutProject,
            string? entityType, int? userId, DateTime? from, DateTime? to, int page, int pageSize)
        {
            var visibleIds = _context.Projects.Where(visibleProjects).Select(p => p.Id);
            var query = _context.AuditLogs.AsNoTracking()
                .Where(a => (a.ProjectId != null && visibleIds.Contains(a.ProjectId.Value))
                            || (includeWithoutProject && a.ProjectId == null));
            return await PageAsync(query, entityType, userId, from, to, page, pageSize);
        }

        private static async Task<(List<AuditLog>, int)> PageAsync(
            IQueryable<AuditLog> query, string? entityType, int? userId, DateTime? from, DateTime? to, int page, int pageSize)
        {
            if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
            if (userId is int uid) query = query.Where(a => a.UserId == uid);
            if (from is DateTime f) query = query.Where(a => a.CreatedAt >= f);
            if (to is DateTime t) query = query.Where(a => a.CreatedAt < t.AddDays(1));

            var total = await query.CountAsync();
            var items = await query.Include(a => a.User)
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }
    }
}
