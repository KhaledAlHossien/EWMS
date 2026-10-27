using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class ProjectService : IProjectService
    {
        private readonly DataContext _context;

        public ProjectService(DataContext context) => _context = context;

        public Task<bool> ExistsForBranchAsync(int branchId) =>
            _context.Projects.AnyAsync(p => p.BranchId == branchId);

        // مشاركة موظفي القسم لا تمنع حذفه (المنع بحارس «له مستخدمون» أصلاً)؛ المانع هنا مشروع يعالجه القسم
        public Task<bool> ExistsForDepartmentAsync(int departmentId) =>
            _context.Projects.AnyAsync(p => p.DepartmentId == departmentId);

        public Task<bool> ExistsForSiteAsync(int siteId) =>
            _context.Projects.AnyAsync(p => p.SiteId == siteId);

        public async Task<bool> ExistsForUserAsync(int userId) =>
            await _context.Projects.AnyAsync(p => p.CreatedByUserId == userId)
            || await _context.ProjectMembers.AnyAsync(m => m.UserId == userId)
            || await _context.ProjectDocuments.AnyAsync(d => d.CreatedByUserId == userId)
            || await _context.ProjectDocumentVersions.AnyAsync(v => v.UploadedByUserId == userId || v.ApprovedByUserId == userId)
            || await _context.AuditLogs.AnyAsync(a => a.UserId == userId);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly DataContext _context;

        public AuditLogService(DataContext context) => _context = context;

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
