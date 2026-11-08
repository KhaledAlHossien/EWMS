using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Application.Features.CentralStudies;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class StudyProjectService : StudyRepositoryBase, IStudyProjectService
    {
        public StudyProjectService(DataContext context) : base(context) { }

        // الفريق (الأقسام والأعضاء) لا يُحمَّل هنا: تحميل كيان User كاملاً لكل عضو يجعل الاستعلام المقسَّم يرتّب صفوفاً عريضة
        // ويطلب ذاكرة تنفيذ كبيرة (8.9 MB لـ 200 عضو) — انظر GetTeamAsync
        private IQueryable<StudyProject> WithDetails() =>
            _context.StudyProjects
                .Include(p => p.StartOrder).ThenInclude(o => o.Response).ThenInclude(r => r.Assignment)
                .Include(p => p.Branch)
                .Include(p => p.Manager)
                .Include(p => p.CreatedBy)
                .Include(p => p.Stages).ThenInclude(s => s.StageType)
                .Include(p => p.Stages).ThenInclude(s => s.ResponsibleDepartment)
                .Include(p => p.Stages).ThenInclude(s => s.ClosedBy)
                .Include(p => p.Stages).ThenInclude(s => s.Tasks)
                .AsSplitQuery();

        public async Task<StudyProject?> GetByIdAsync(int id) =>
            await WithDetails().FirstOrDefaultAsync(p => p.Id == id);

        /// <summary>
        /// الأقسام المشاركة وأعضاء فريقها كإسقاط مباشر إلى DTO: الاسم فقط من المستخدم (العضو ومن عيّنه ومن أضاف القسم)
        /// بلا تحميل كيان User ولا تتبّع — الواجهة لا تحتاج غير الأسماء. استعلامان مسطَّحان بلا ORDER BY في SQL
        /// (الترتيب والتجميع في الذاكرة): ترتيب صفوف عريضة (أسماء nvarchar(max)) هو ما كان يطلب ذاكرة تنفيذ كبيرة.
        /// </summary>
        public async Task<List<ProjectDepartmentDto>> GetTeamAsync(int projectId)
        {
            var departments = await _context.ProjectDepartments.AsNoTracking()
                .Where(d => d.ProjectId == projectId)
                .Select(d => new ProjectDepartmentDto
                {
                    Id = d.Id,
                    DepartmentId = d.DepartmentId,
                    DepartmentName = d.Department.Name,
                    AddedByName = d.AddedBy.FullName,
                    AddedAt = d.AddedAt
                })
                .ToListAsync();

            var members = await _context.ProjectMembers.AsNoTracking()
                .Where(m => m.ProjectDepartment.ProjectId == projectId)
                .Select(m => new
                {
                    m.ProjectDepartmentId,
                    Member = new ProjectMemberDto
                    {
                        UserId = m.UserId,
                        FullName = m.User.FullName,
                        RoleTitle = m.RoleTitle,
                        AssignedByName = m.AssignedBy.FullName,
                        AssignedAt = m.AssignedAt
                    }
                })
                .ToListAsync();

            var byDepartment = members.ToLookup(x => x.ProjectDepartmentId, x => x.Member);
            foreach (var d in departments)
                d.Members = byDepartment[d.Id].OrderBy(m => m.AssignedAt).ThenBy(m => m.UserId).ToList();
            return departments.OrderBy(d => d.AddedAt).ThenBy(d => d.Id).ToList();
        }

        public async Task<(List<StudyProjectDto> Items, int Total)> SearchAsync(
            StudyDocumentFilterDto filter, int? branchLimit, int page, int pageSize)
        {
            var query = _context.StudyProjects.AsQueryable();
            if (branchLimit != null) query = query.Where(p => p.BranchId == branchLimit);
            if (filter.BranchId is int branchId) query = query.Where(p => p.BranchId == branchId);
            if (filter.Status is int status) query = query.Where(p => (int)p.Status == status);
            if (filter.From is DateTime from) query = query.Where(p => p.PlannedStartDate >= from.Date);
            if (filter.To is DateTime to) query = query.Where(p => p.PlannedStartDate <= to.Date);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                query = query.Where(p => p.Code.Contains(text) || p.Name.Contains(text));
            }

            var total = await query.CountAsync();
            var ids = await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(p => p.Id).ToListAsync();
            if (ids.Count == 0) return ([], total);

            // ثلاثة استعلامات مسطّحة بلا ORDER BY (التجميع والترتيب في الذاكرة): المشاريع، مراحلها، وأرقام مهامها فقط
            var projects = await _context.StudyProjects.AsNoTracking().Where(p => ids.Contains(p.Id))
                .Select(p => new StudyProjectDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    StartOrderId = p.StartOrderId,
                    StartOrderNumber = p.StartOrder.OrderNumber,
                    BranchId = p.BranchId,
                    BranchName = p.Branch.Name,
                    SiteId = p.SiteId,
                    ManagerUserId = p.ManagerUserId,
                    ManagerName = p.Manager.FullName,
                    PlannedStartDate = p.PlannedStartDate,
                    PlannedEndDate = p.PlannedEndDate,
                    ActualStartDate = p.ActualStartDate,
                    ActualEndDate = p.ActualEndDate,
                    Status = (int)p.Status,
                    CreatedByName = p.CreatedBy.FullName,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    RowVersion = p.RowVersion
                })
                .ToListAsync();

            var stages = await _context.ProjectStages.AsNoTracking().Where(s => ids.Contains(s.ProjectId))
                .Select(s => new ProjectStageDto
                {
                    Id = s.Id,
                    ProjectId = s.ProjectId,
                    StageTypeId = s.StageTypeId,
                    StageTypeName = s.StageType.Name,
                    LinkedDocument = (int)s.StageType.LinkedDocument,
                    SortOrder = s.SortOrder,
                    Weight = s.Weight,
                    PlannedStartDate = s.PlannedStartDate,
                    PlannedEndDate = s.PlannedEndDate,
                    ActualStartDate = s.ActualStartDate,
                    ActualEndDate = s.ActualEndDate,
                    Status = (int)s.Status,
                    ResponsibleDepartmentId = s.ResponsibleDepartmentId,
                    ResponsibleDepartmentName = s.ResponsibleDepartment == null ? null : s.ResponsibleDepartment.Name,
                    ClosedByName = s.ClosedBy == null ? null : s.ClosedBy.FullName,
                    ClosedAt = s.ClosedAt,
                    RowVersion = s.RowVersion
                })
                .ToListAsync();

            var tasks = await _context.StageTasks.AsNoTracking().Where(t => ids.Contains(t.Stage.ProjectId))
                .Select(t => new { t.StageId, t.Progress, t.Weight, t.Status })
                .ToListAsync();
            var tasksByStage = tasks.ToLookup(t => t.StageId);
            var stagesByProject = stages.ToLookup(s => s.ProjectId);

            // نسب التقدم بقاعدة StudyRules نفسها (المرحلة المغلقة = 100، والباقي متوسط موزون لمهامها)
            foreach (var stage in stages)
            {
                var stageTasks = tasksByStage[stage.Id].ToList();
                stage.TasksTotal = stageTasks.Count;
                stage.TasksDone = stageTasks.Count(t => t.Status == StudyTaskStatus.Done);
                stage.StatusName = StudyRules.Name((StudyStageStatus)stage.Status);
                stage.Progress = (StudyStageStatus)stage.Status == StudyStageStatus.Closed
                    ? 100
                    : StudyRules.Weighted(stageTasks.Select(t => (t.Progress, t.Weight)));
            }
            foreach (var project in projects)
            {
                project.Stages = stagesByProject[project.Id].OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToList();
                project.StatusName = StudyRules.Name((StudyProjectStatus)project.Status);
                project.Progress = StudyRules.Weighted(project.Stages.Select(s => (s.Progress, s.Weight)));
            }
            return (projects.OrderBy(p => ids.IndexOf(p.Id)).ToList(), total);
        }

        public async Task<bool> ExistsForStartOrderAsync(int startOrderId) =>
            await _context.StudyProjects.AnyAsync(p => p.StartOrderId == startOrderId);

        public async Task<bool> CodeExistsAsync(string code, int? excludeId = null) =>
            await _context.StudyProjects.AnyAsync(p => p.Code == code && p.Id != excludeId);

        public async Task<bool> HasContentAsync(int projectId) =>
            await _context.StageTasks.AnyAsync(t => t.Stage.ProjectId == projectId)
            || await _context.ProjectDrawings.AnyAsync(d => d.ProjectId == projectId)
            || await _context.StudyAttachments.AnyAsync(a => a.ProjectId == projectId);

        public async Task<int> GetRequestIdAsync(int projectId) =>
            await _context.StudyProjects.Where(p => p.Id == projectId)
                .Select(p => p.StartOrder.Response.Assignment.RequestId).FirstOrDefaultAsync();

        public async Task<StudyProject> AddAsync(StudyProject project)
        {
            _context.StudyProjects.Add(project);
            await SaveChangesAsync();
            return project;
        }

        public async Task UpdateAsync(StudyProject project, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(project, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteAsync(StudyProject project)
        {
            _context.StudyProjects.Remove(project);
            await SaveChangesAsync();
        }

        // ----- الأقسام المشاركة والفريق -----
        public async Task<ProjectDepartment?> GetProjectDepartmentAsync(int id) =>
            await _context.ProjectDepartments
                .Include(d => d.Project)
                .Include(d => d.Department)
                .Include(d => d.Members)
                .FirstOrDefaultAsync(d => d.Id == id);

        public async Task<bool> DepartmentParticipatesAsync(int projectId, int departmentId) =>
            await _context.ProjectDepartments.AnyAsync(d => d.ProjectId == projectId && d.DepartmentId == departmentId);

        public async Task<bool> HasTasksForDepartmentAsync(int projectId, int departmentId) =>
            await _context.StageTasks.AnyAsync(t => t.Stage.ProjectId == projectId
                && (t.DepartmentId == departmentId || (t.Office != null && t.Office.DepartmentId == departmentId)));

        public async Task<bool> IsMemberAsync(int projectId, int userId) =>
            await _context.ProjectMembers.AnyAsync(m => m.ProjectDepartment.ProjectId == projectId && m.UserId == userId);

        public async Task<bool> HasTasksForUserAsync(int projectId, int userId) =>
            await _context.StageTasks.AnyAsync(t => t.Stage.ProjectId == projectId && t.AssigneeUserId == userId);

        public async Task<List<Domain.Entities.Office>> GetOfficesOfDepartmentsAsync(IEnumerable<int> departmentIds)
        {
            var ids = departmentIds.Distinct().ToList();
            if (ids.Count == 0) return [];
            return await _context.Offices.Where(o => ids.Contains(o.DepartmentId))
                .OrderBy(o => o.Name).ThenBy(o => o.Id).AsNoTracking().ToListAsync();
        }

        public async Task AddProjectDepartmentAsync(ProjectDepartment projectDepartment)
        {
            _context.ProjectDepartments.Add(projectDepartment);
            await SaveChangesAsync();
        }

        public async Task RemoveProjectDepartmentAsync(ProjectDepartment projectDepartment)
        {
            _context.ProjectDepartments.Remove(projectDepartment);
            await SaveChangesAsync();
        }

        public async Task AddMemberAsync(ProjectMember member)
        {
            _context.ProjectMembers.Add(member);
            await SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(ProjectMember member)
        {
            _context.ProjectMembers.Remove(member);
            await SaveChangesAsync();
        }

        public Task SaveAsync() => SaveChangesAsync();
    }

    public class StudyStageService : StudyRepositoryBase, IStudyStageService
    {
        public StudyStageService(DataContext context) : base(context) { }

        public async Task<ProjectStage?> GetStageAsync(int id) =>
            await _context.ProjectStages
                .Include(s => s.Project)
                .Include(s => s.StageType)
                .Include(s => s.ResponsibleDepartment)
                .Include(s => s.ClosedBy)
                .Include(s => s.Tasks)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<List<ProjectStage>> GetStagesAsync(int projectId) =>
            await _context.ProjectStages.Where(s => s.ProjectId == projectId)
                .Include(s => s.StageType)
                .Include(s => s.ResponsibleDepartment)
                .Include(s => s.ClosedBy)
                .Include(s => s.Tasks)
                .AsSplitQuery()
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .ToListAsync();

        public async Task AddStageAsync(ProjectStage stage)
        {
            _context.ProjectStages.Add(stage);
            await SaveChangesAsync();
        }

        public async Task UpdateStageAsync(ProjectStage stage, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(stage, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteStageAsync(ProjectStage stage)
        {
            _context.ProjectStages.Remove(stage);
            await SaveChangesAsync();
        }

        private IQueryable<StageTask> TasksWithDetails() => _context.StageTasks
            .Include(t => t.Stage).ThenInclude(s => s.Project)
            .Include(t => t.Stage).ThenInclude(s => s.StageType)
            .Include(t => t.Department)
            .Include(t => t.Office)
            .Include(t => t.Assignee)
            .Include(t => t.AssignedBy);

        public async Task<StageTask?> GetTaskAsync(int id) =>
            await TasksWithDetails().FirstOrDefaultAsync(t => t.Id == id);

        public async Task<List<StageTask>> GetTasksAsync(int stageId) =>
            await TasksWithDetails().Where(t => t.StageId == stageId)
                .OrderBy(t => t.Status).ThenBy(t => t.PlannedEndDate).ThenBy(t => t.Id)
                .AsNoTracking().ToListAsync();

        // المهام بلا بدء مخطط في آخر مرحلتها
        public async Task<(List<StageTask> Items, int Total)> GetProjectTasksAsync(int projectId, int limit)
        {
            var total = await _context.StageTasks.CountAsync(t => t.Stage.ProjectId == projectId);
            var items = await TasksWithDetails().Where(t => t.Stage.ProjectId == projectId)
                .OrderBy(t => t.Stage.SortOrder).ThenBy(t => t.StageId)
                .ThenBy(t => t.PlannedStartDate == null).ThenBy(t => t.PlannedStartDate).ThenBy(t => t.Id)
                .Take(limit)
                .AsNoTracking().ToListAsync();
            return (items, total);
        }

        public async Task<(List<StageTask> Items, int Total)> GetAssignedAsync(
            int userId, int? officeId, int? departmentId, bool openOnly, int page, int pageSize)
        {
            var query = _context.StageTasks.Where(t =>
                t.AssigneeUserId == userId
                || (officeId != null && t.OfficeId == officeId)
                || (departmentId != null && officeId == null && t.DepartmentId == departmentId));
            if (openOnly) query = query.Where(t => t.Status != StudyTaskStatus.Done);
            query = query.Where(t => t.Stage.Project.Status != StudyProjectStatus.Cancelled);

            var total = await query.CountAsync();
            var ids = await query.OrderBy(t => t.Status).ThenBy(t => t.PlannedEndDate).ThenByDescending(t => t.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(t => t.Id).ToListAsync();
            var items = await TasksWithDetails().Where(t => ids.Contains(t.Id)).AsNoTracking().ToListAsync();
            return (items.OrderBy(t => ids.IndexOf(t.Id)).ToList(), total);
        }

        public async Task AddTaskAsync(StageTask task)
        {
            _context.StageTasks.Add(task);
            await SaveChangesAsync();
        }

        public async Task UpdateTaskAsync(StageTask task, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(task, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteTaskAsync(StageTask task)
        {
            _context.StageTasks.Remove(task);
            await SaveChangesAsync();
        }

        public Task SaveAsync() => SaveChangesAsync();
    }
}
