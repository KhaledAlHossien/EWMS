using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.Drawings
{
    /// <summary>الدوال المشتركة بين عمليات Drawings (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyDrawingSupport
    {
        private readonly IStudyDrawingService _service;
        private readonly IStudyProjectService _projects;
        private readonly IStudyApprovalService _approvals;
        private readonly StudyKit _kit;

        public StudyDrawingSupport(IStudyDrawingService service, IStudyProjectService projects, IStudyApprovalService approvals, StudyKit kit)
        {
            _service = service;
            _projects = projects;
            _approvals = approvals;
            _kit = kit;
        }

        public StudyDrawingDto ToDto(ProjectDrawing d, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyDrawingDto>(d);
            var inScope = StudyRules.InScope(viewer, d.Project.BranchId);
            dto.CanEdit = inScope && IsOpen(d.Project) && viewer.Has(StudyPermissions.EditDrawing)
                          && StudyRules.InDepartment(viewer, d.DepartmentId) && d.Status != DrawingStatus.Approved;
            dto.CanReview = inScope && IsOpen(d.Project) && viewer.Has("ReviewStudyDrawing")
                            && d.Kind != DrawingKind.Architectural && d.Status == DrawingStatus.PendingReview;
            return dto;
        }

        public async Task<(DesignApproval Cda, int RequestId)> LoadCdaAsync(int id, Viewer viewer)
        {
            var cda = await _service.GetDesignApprovalAsync(id) ?? throw new KeyNotFoundException("اعتماد المخططات غير موجود");
            StudyRules.EnsureInScope(viewer, cda.Version.Drawing.Project.BranchId);
            return (cda, await _projects.GetRequestIdAsync(cda.Version.Drawing.ProjectId));
        }

        public async Task EnsureNoDecisionsAsync(DesignApproval cda)
        {
            var rows = await _approvals.GetForOwnerAsync(StudyEntityType.DesignApproval, cda.Id);
            if (rows.Any(r => r.Decision != StudyDecision.Pending))
                throw new InvalidOperationException("بدأ التوقيع على الاعتماد — لا يُعدَّل ولا يُحذف");
        }

        /// <summary>الأساس لغير المعماري: نسخة معمارية معتمدة من المشروع نفسه</summary>
        public async Task EnsureBasisAsync(DrawingKind kind, int? versionId, int projectId)
        {
            if (versionId is not int id) return;
            if (kind == DrawingKind.Architectural)
                throw new InvalidOperationException("المخطط المعماري لا يُبنى على نسخة أخرى");
            var version = await _service.GetVersionAsync(id) ?? throw new KeyNotFoundException("النسخة المعمارية غير موجودة");
            if (version.Drawing.ProjectId != projectId)
                throw new InvalidOperationException("النسخة المعمارية من مشروع آخر");
            if (version.Status != DrawingStatus.Approved)
                throw new InvalidOperationException("يُبنى المخطط على نسخة معمارية معتمدة فقط");
        }

        public static void EnsureOwnDepartment(Viewer viewer, ProjectDrawing d)
        {
            if (!StudyRules.InDepartment(viewer, d.DepartmentId))
                throw new UnauthorizedAccessException("يعدّل المخطط قسمه المُصدِر فقط");
        }

        public async Task<ArchitecturalVersion> LoadVersionForEditAsync(int id, Viewer viewer)
        {
            var version = await _service.GetVersionAsync(id) ?? throw new KeyNotFoundException("النسخة غير موجودة");
            StudyRules.EnsureInScope(viewer, version.Drawing.Project.BranchId);
            EnsureOpen(version.Drawing.Project);
            EnsureOwnDepartment(viewer, version.Drawing);
            if (version.DesignApproval != null)
                throw new InvalidOperationException("صدر للنسخة اعتماد مخططات — لا تُعدَّل ولا تُحذف (أصدر نسخة جديدة)");
            return version;
        }

        public async Task<DesignApprovalDto> CdaDetailAsync(int id, Viewer viewer)
        {
            var (cda, requestId) = await LoadCdaAsync(id, viewer);
            var dto = _kit.Mapper.Map<DesignApprovalDto>(cda);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(cda, requestId));
            dto.CanEdit = viewer.Has("EditDesignApproval") && IsOpen(cda.Version.Drawing.Project)
                          && dto.Approvals.All(a => a.Decision == (int)StudyDecision.Pending);
            return dto;
        }

        public async Task LogCoreAsync(int projectId, StudyEntityType type, int id, StudyLogAction action, string title, string? details) =>
            await _kit.LogAsync(await _projects.GetRequestIdAsync(projectId), type, id, action, title, details);

        public static bool IsOpen(StudyProject p) => p.Status is StudyProjectStatus.Running or StudyProjectStatus.Suspended;

        public async Task<ProjectDrawing> LoadAsync(int id, Viewer viewer)
        {
            var drawing = await _service.GetDrawingAsync(id) ?? throw new KeyNotFoundException("المخطط غير موجود");
            StudyRules.EnsureInScope(viewer, drawing.Project.BranchId);
            return drawing;
        }

        public async Task<StudyDrawingDto> DetailAsync(int id, Viewer viewer) => ToDto(await LoadAsync(id, viewer), viewer);

        public static void EnsureOpen(StudyProject p)
        {
            if (!IsOpen(p)) throw new InvalidOperationException("المشروع مكتمل أو ملغى — لا تُعدَّل مخططاته");
        }

        public Task LogAsync(int projectId, StudyEntityType type, int id, StudyLogAction action, string title, string? details = null) =>
            LogCoreAsync(projectId, type, id, action, title, details);
    }
}
