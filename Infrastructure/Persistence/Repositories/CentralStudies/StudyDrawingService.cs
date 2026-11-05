using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class StudyDrawingService : StudyRepositoryBase, IStudyDrawingService
    {
        public StudyDrawingService(DataContext context) : base(context) { }

        private IQueryable<ProjectDrawing> WithDetails() => _context.ProjectDrawings
            .Include(d => d.Project)
            .Include(d => d.Department)
            .Include(d => d.Office)
            .Include(d => d.PreparedBy)
            .Include(d => d.BasedOnVersion);

        public async Task<ProjectDrawing?> GetDrawingAsync(int id)
        {
            var drawing = await WithDetails().FirstOrDefaultAsync(d => d.Id == id);
            // نسخ المعماري واعتماداتها (العلاقة على الصنف المشتق)
            if (drawing is ArchitecturalDrawing architectural)
                await _context.Entry(architectural).Collection(a => a.Versions).Query()
                    .Include(v => v.CreatedBy).Include(v => v.DesignApproval).LoadAsync();
            return drawing;
        }

        public async Task<List<ProjectDrawing>> GetByProjectAsync(int projectId, DrawingKind? kind) =>
            await WithDetails().Where(d => d.ProjectId == projectId && (kind == null || d.Kind == kind))
                .OrderBy(d => d.Kind).ThenBy(d => d.DrawingNumber)
                .AsNoTracking().ToListAsync();

        public async Task<bool> DrawingNumberExistsAsync(int projectId, string number, int? excludeId = null) =>
            await _context.ProjectDrawings.AnyAsync(d => d.ProjectId == projectId && d.DrawingNumber == number && d.Id != excludeId);

        public async Task<bool> HasVersionsAsync(int drawingId) =>
            await _context.ArchitecturalVersions.AnyAsync(v => v.DrawingId == drawingId);

        public async Task AddDrawingAsync(ProjectDrawing drawing)
        {
            _context.ProjectDrawings.Add(drawing);
            await SaveChangesAsync();
        }

        public async Task UpdateDrawingAsync(ProjectDrawing drawing, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(drawing, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteDrawingAsync(ProjectDrawing drawing)
        {
            _context.ProjectDrawings.Remove(drawing);
            await SaveChangesAsync();
        }

        // ----- نسخ المعماري -----
        public async Task<ArchitecturalVersion?> GetVersionAsync(int id) =>
            await _context.ArchitecturalVersions
                .Include(v => v.Drawing).ThenInclude(d => d.Project)
                .Include(v => v.CreatedBy)
                .Include(v => v.DesignApproval)
                .FirstOrDefaultAsync(v => v.Id == id);

        public async Task<int> NextVersionNumberAsync(int drawingId) =>
            (await _context.ArchitecturalVersions.Where(v => v.DrawingId == drawingId)
                .MaxAsync(v => (int?)v.VersionNumber) ?? 0) + 1;

        public async Task<bool> VersionUsedAsBasisAsync(int versionId) =>
            await _context.ProjectDrawings.AnyAsync(d => d.BasedOnVersionId == versionId);

        public async Task AddVersionAsync(ArchitecturalVersion version)
        {
            _context.ArchitecturalVersions.Add(version);
            await SaveChangesAsync();
        }

        public async Task UpdateVersionAsync(ArchitecturalVersion version, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(version, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteVersionAsync(ArchitecturalVersion version)
        {
            _context.ArchitecturalVersions.Remove(version);
            await SaveChangesAsync();
        }

        // ----- اعتماد المخططات الأولي -----
        public async Task<DesignApproval?> GetDesignApprovalAsync(int id) =>
            await _context.DesignApprovals
                .Include(a => a.Version).ThenInclude(v => v.Drawing).ThenInclude(d => d.Project)
                .Include(a => a.CreatedBy)
                .FirstOrDefaultAsync(a => a.Id == id);

        public async Task AddDesignApprovalAsync(DesignApproval approval)
        {
            _context.DesignApprovals.Add(approval);
            await SaveChangesAsync();
        }

        public async Task UpdateDesignApprovalAsync(DesignApproval approval, byte[]? expectedRowVersion = null)
        {
            ExpectVersion(approval, expectedRowVersion);
            await SaveChangesAsync();
        }

        public async Task DeleteDesignApprovalAsync(DesignApproval approval)
        {
            _context.DesignApprovals.Remove(approval);
            await SaveChangesAsync();
        }

        public Task SaveAsync() => SaveChangesAsync();
    }
}
