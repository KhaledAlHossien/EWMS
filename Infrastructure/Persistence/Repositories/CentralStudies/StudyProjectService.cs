using Application.DTOs.Request;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class StudyProjectService : StudyRepositoryBase, IStudyProjectService
    {
        public StudyProjectService(DataContext context) : base(context) { }

        private IQueryable<StudyProject> WithDetails(bool withTeam)
        {
            var query = _context.StudyProjects
                .Include(p => p.StartOrder).ThenInclude(o => o.Response).ThenInclude(r => r.Assignment)
                .Include(p => p.Branch)
                .Include(p => p.Manager)
                .Include(p => p.CreatedBy)
                .Include(p => p.Stages).ThenInclude(s => s.StageType)
                .Include(p => p.Stages).ThenInclude(s => s.ResponsibleDepartment)
                .Include(p => p.Stages).ThenInclude(s => s.ClosedBy)
                .Include(p => p.Stages).ThenInclude(s => s.Tasks)
                .AsSplitQuery();
            if (withTeam)
                query = query
                    .Include(p => p.Departments).ThenInclude(d => d.Department)
                    .Include(p => p.Departments).ThenInclude(d => d.AddedBy)
                    .Include(p => p.Departments).ThenInclude(d => d.Members).ThenInclude(m => m.User)
                    .Include(p => p.Departments).ThenInclude(d => d.Members).ThenInclude(m => m.AssignedBy);
            return query;
        }

        public async Task<StudyProject?> GetByIdAsync(int id, bool withTeam = false) =>
            await WithDetails(withTeam).FirstOrDefaultAsync(p => p.Id == id);

        public async Task<(List<StudyProject> Items, int Total)> SearchAsync(
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
            var items = await WithDetails(false).Where(p => ids.Contains(p.Id)).AsNoTracking().ToListAsync();
            return (items.OrderBy(p => ids.IndexOf(p.Id)).ToList(), total);
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
