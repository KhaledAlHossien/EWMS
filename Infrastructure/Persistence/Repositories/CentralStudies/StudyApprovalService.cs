using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class StudyApprovalService : StudyRepositoryBase, IStudyApprovalService
    {
        public StudyApprovalService(DataContext context) : base(context) { }

        private IQueryable<StudyApproval> ForOwner(StudyEntityType ownerType, int ownerId) => ownerType switch
        {
            StudyEntityType.Assignment => _context.StudyApprovals.Where(a => a.AssignmentId == ownerId),
            StudyEntityType.Response => _context.StudyApprovals.Where(a => a.ResponseId == ownerId),
            StudyEntityType.StartOrder => _context.StudyApprovals.Where(a => a.StartOrderId == ownerId),
            StudyEntityType.DesignApproval => _context.StudyApprovals.Where(a => a.DesignApprovalId == ownerId),
            StudyEntityType.Project => _context.StudyApprovals.Where(a => a.ProjectId == ownerId),
            _ => _context.StudyApprovals.Where(a => false)
        };

        /// <summary>السطر مع مستنده حتى الفرع والقسم المعني بالصفة</summary>
        private IQueryable<StudyApproval> WithOwners() => _context.StudyApprovals
            .Include(a => a.Signer)
            .Include(a => a.Assignment).ThenInclude(x => x!.Request).ThenInclude(r => r.Branch)
            .Include(a => a.Response).ThenInclude(x => x!.Assignment).ThenInclude(x => x.Request).ThenInclude(r => r.Branch)
            .Include(a => a.StartOrder).ThenInclude(x => x!.Response).ThenInclude(x => x.Assignment).ThenInclude(x => x.Request).ThenInclude(r => r.Branch)
            .Include(a => a.DesignApproval).ThenInclude(x => x!.Version).ThenInclude(v => v.Drawing).ThenInclude(d => d.Project).ThenInclude(p => p.Branch)
            .Include(a => a.DesignApproval).ThenInclude(x => x!.Version).ThenInclude(v => v.Drawing).ThenInclude(d => d.Project)
                .ThenInclude(p => p.StartOrder).ThenInclude(o => o.Response).ThenInclude(r => r.Assignment)
            .Include(a => a.Project).ThenInclude(p => p!.Branch)
            .Include(a => a.Project).ThenInclude(p => p!.StartOrder).ThenInclude(o => o.Response).ThenInclude(r => r.Assignment)
            .AsSplitQuery();

        public async Task<List<StudyApproval>> GetForOwnerAsync(StudyEntityType ownerType, int ownerId) =>
            await ForOwner(ownerType, ownerId).Include(a => a.Signer)
                .OrderBy(a => a.SortOrder).AsNoTracking().ToListAsync();

        public async Task<StudyApproval?> GetWithOwnerAsync(int id) =>
            await WithOwners().FirstOrDefaultAsync(a => a.Id == id);

        public async Task<List<StudyApproval>> GetPendingAsync(IReadOnlyCollection<StudySignerRole> roles)
        {
            var list = roles.ToList();
            return await WithOwners().Where(a => a.Decision == StudyDecision.Pending && list.Contains(a.SignerRole))
                .OrderBy(a => a.Id).AsNoTracking().ToListAsync();
        }

        public async Task ReplaceForOwnerAsync(StudyEntityType ownerType, int ownerId, List<StudyApproval> rows)
        {
            var existing = await ForOwner(ownerType, ownerId).ToListAsync();
            _context.StudyApprovals.RemoveRange(existing);
            _context.StudyApprovals.AddRange(rows);
        }

        public Task SaveAsync() => SaveChangesAsync();
    }

    public class StudyAttachmentService : StudyRepositoryBase, IStudyAttachmentService
    {
        public StudyAttachmentService(DataContext context) : base(context) { }

        private IQueryable<StudyAttachment> ForOwner(StudyEntityType ownerType, int ownerId) => ownerType switch
        {
            StudyEntityType.Request => _context.StudyAttachments.Where(a => a.RequestId == ownerId),
            StudyEntityType.Assignment => _context.StudyAttachments.Where(a => a.AssignmentId == ownerId),
            StudyEntityType.Response => _context.StudyAttachments.Where(a => a.ResponseId == ownerId),
            StudyEntityType.StartOrder => _context.StudyAttachments.Where(a => a.StartOrderId == ownerId),
            StudyEntityType.Project => _context.StudyAttachments.Where(a => a.ProjectId == ownerId),
            StudyEntityType.Drawing => _context.StudyAttachments.Where(a => a.DrawingId == ownerId),
            StudyEntityType.Version => _context.StudyAttachments.Where(a => a.VersionId == ownerId),
            _ => _context.StudyAttachments.Where(a => false)
        };

        public async Task<List<StudyAttachment>> GetForOwnerAsync(StudyEntityType ownerType, int ownerId) =>
            await ForOwner(ownerType, ownerId)
                .Include(a => a.AttachmentType).Include(a => a.UploadedBy)
                .OrderByDescending(a => a.UploadedAt).AsNoTracking().ToListAsync();

        public async Task<StudyAttachment?> GetByIdAsync(int id) =>
            await _context.StudyAttachments
                .Include(a => a.AttachmentType).Include(a => a.UploadedBy)
                .FirstOrDefaultAsync(a => a.Id == id);

        public async Task<int> CountForOwnerAsync(StudyEntityType ownerType, int ownerId) =>
            await ForOwner(ownerType, ownerId).CountAsync();

        public async Task AddAsync(StudyAttachment attachment)
        {
            _context.StudyAttachments.Add(attachment);
            await SaveChangesAsync();
        }

        public async Task DeleteAsync(StudyAttachment attachment)
        {
            _context.StudyAttachments.Remove(attachment);
            await SaveChangesAsync();
        }
    }

    public class StudyActivityLogService : IStudyActivityLogService
    {
        private readonly DataContext _context;

        public StudyActivityLogService(DataContext context)
        {
            _context = context;
        }

        public async Task AddAsync(StudyActivityLog log)
        {
            _context.StudyActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task<(List<Application.DTOs.Response.StudyActivityLogDto> Items, int Total)> GetForRequestAsync(
            int requestId, int page, int pageSize, int? projectId = null,
            System.Linq.Expressions.Expression<Func<StageTask, bool>>? visibleTasks = null)
        {
            var query = _context.StudyActivityLogs.AsNoTracking().Where(l => l.RequestId == requestId);
            if (visibleTasks != null && projectId is int project)
            {
                // سجل بحدّ الرؤية: تُخفى أحداث المشروع والمراحل والأقسام المشاركة والفريق، وتبقى مهام يراها فقط؛
                // ومرفقات المشروع تُخفى (الموجود منها بالمالك، والمحذوف بخاتمة عنوان السجل «… مشروع»). الوثائق والمخططات كما هي
                var visibleTaskIds = _context.StageTasks.Where(t => t.Stage.ProjectId == project).Where(visibleTasks).Select(t => t.Id);
                var projectAttachmentIds = _context.StudyAttachments.Where(a => a.ProjectId != null).Select(a => a.Id);
                var existingAttachmentIds = _context.StudyAttachments.Select(a => a.Id);
                var projectName = Application.Features.CentralStudies.StudyRules.Name(StudyEntityType.Project);
                query = query.Where(l =>
                    l.EntityType != StudyEntityType.Project && l.EntityType != StudyEntityType.Stage
                    && l.EntityType != StudyEntityType.ProjectDepartment && l.EntityType != StudyEntityType.ProjectMember
                    && (l.EntityType != StudyEntityType.Task || visibleTaskIds.Contains(l.EntityId))
                    && (l.EntityType != StudyEntityType.Attachment
                        || !(projectAttachmentIds.Contains(l.EntityId)
                             || (!existingAttachmentIds.Contains(l.EntityId) && l.Title.EndsWith(projectName)))));
            }
            var total = await query.CountAsync();   // يخدمه الفهرس (RequestId, CreatedAt)
            var rows = await query
                .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(l => new
                {
                    l.Id, l.EntityType, l.EntityId, l.Action, l.Title, l.Details, l.UserId,
                    UserName = l.User.FullName, l.CreatedAt
                })
                .ToListAsync();
            var items = rows.Select(r => new Application.DTOs.Response.StudyActivityLogDto
            {
                Id = r.Id,
                EntityType = (int)r.EntityType,
                EntityTypeName = Application.Features.CentralStudies.StudyRules.Name(r.EntityType),
                EntityId = r.EntityId,
                Action = (int)r.Action,
                ActionName = Application.Features.CentralStudies.StudyRules.Name(r.Action),
                Title = r.Title,
                Details = r.Details,
                UserId = r.UserId,
                UserName = r.UserName,
                CreatedAt = r.CreatedAt
            }).ToList();
            return (items, total);
        }
    }

    public class StudyReferenceService : IStudyReferenceService
    {
        private readonly DataContext _context;

        public StudyReferenceService(DataContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.StudyRequests.AnyAsync(x => x.CreatedByUserId == userId)
            || await _context.StudyAssignments.AnyAsync(x => x.CreatedByUserId == userId)
            || await _context.StudyAssignmentResponses.AnyAsync(x => x.CreatedByUserId == userId || x.ProposedManagerUserId == userId)
            || await _context.StudyStartOrders.AnyAsync(x => x.CreatedByUserId == userId || x.ProjectManagerUserId == userId)
            || await _context.StartOrderEngineers.AnyAsync(x => x.EngineerUserId == userId)
            || await _context.StudyProjects.AnyAsync(x => x.CreatedByUserId == userId || x.ManagerUserId == userId)
            || await _context.ProjectDepartments.AnyAsync(x => x.AddedByUserId == userId)
            || await _context.ProjectMembers.AnyAsync(x => x.UserId == userId || x.AssignedByUserId == userId)
            || await _context.ProjectStages.AnyAsync(x => x.ClosedByUserId == userId)
            || await _context.StageTasks.AnyAsync(x => x.AssigneeUserId == userId || x.AssignedByUserId == userId)
            || await _context.ProjectDrawings.AnyAsync(x => x.PreparedByUserId == userId)
            || await _context.ArchitecturalVersions.AnyAsync(x => x.CreatedByUserId == userId)
            || await _context.DesignApprovals.AnyAsync(x => x.CreatedByUserId == userId)
            || await _context.StudyApprovals.AnyAsync(x => x.SignerUserId == userId)
            || await _context.StudyAttachments.AnyAsync(x => x.UploadedByUserId == userId)
            || await _context.StudyActivityLogs.AnyAsync(x => x.UserId == userId);

        public async Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            await _context.StudyAssignments.AnyAsync(x => x.FollowingDepartmentId == departmentId)
            || await _context.StudyStartOrders.AnyAsync(x => x.CompetentDepartmentId == departmentId)
            || await _context.ProjectDepartments.AnyAsync(x => x.DepartmentId == departmentId)
            || await _context.ProjectStages.AnyAsync(x => x.ResponsibleDepartmentId == departmentId)
            || await _context.StageTasks.AnyAsync(x => x.DepartmentId == departmentId)
            || await _context.ProjectDrawings.AnyAsync(x => x.DepartmentId == departmentId);

        public async Task<bool> ExistsForOfficeAsync(int officeId) =>
            await _context.StageTasks.AnyAsync(x => x.OfficeId == officeId)
            || await _context.ProjectDrawings.AnyAsync(x => x.OfficeId == officeId);

        public async Task<bool> ExistsForBranchAsync(int branchId) =>
            await _context.StudyRequests.AnyAsync(x => x.BranchId == branchId)
            || await _context.StudyProjects.AnyAsync(x => x.BranchId == branchId);

        public async Task<bool> ExistsForSiteAsync(int siteId) =>
            await _context.StudyRequests.AnyAsync(x => x.SiteId == siteId);
    }
}
