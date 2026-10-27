using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Projects
{
    // وثائق المشاريع: الصلاحية (في المتحكم) تفتح العملية، وحدّ الوثيقة هو حدّ رؤية مشروعها (ProjectRules.Visible) يُفحص هنا لكل وثيقة.
    // الرفع في ProjectUploadFeature؛ هنا القراءة والاعتماد والحذف وفتح الملف.
    public record SearchProjectDocumentsQuery(int? ProjectId, int? Kind, string? Search, DateTime? From, DateTime? To, int Page, int PageSize)
        : IRequest<PagedResultDto<ProjectDocumentResponseDto>>;
    public record GetProjectDocumentQuery(int Id) : IRequest<ProjectDocumentResponseDto>;
    public record ApproveProjectDocumentVersionCommand(int VersionId) : IRequest<ProjectDocumentResponseDto>;
    public record DeleteProjectDocumentCommand(int Id) : IRequest<Unit>;
    /// <summary>FirstRequest: أول طلب لفتح الملف (بلا Range أو من البايت 0) — للتدقيق فقط</summary>
    public record GetProjectDocumentFileQuery(int VersionId, bool Download, bool FirstRequest) : IRequest<ProjectDocumentFileDto>;

    public record ProjectDocumentFileDto(string FileName, string ContentType, Stream Content, bool Inline);

    public class ProjectDocumentHandler :
        IRequestHandler<SearchProjectDocumentsQuery, PagedResultDto<ProjectDocumentResponseDto>>,
        IRequestHandler<GetProjectDocumentQuery, ProjectDocumentResponseDto>,
        IRequestHandler<ApproveProjectDocumentVersionCommand, ProjectDocumentResponseDto>,
        IRequestHandler<DeleteProjectDocumentCommand, Unit>,
        IRequestHandler<GetProjectDocumentFileQuery, ProjectDocumentFileDto>
    {
        private readonly IProjectDocumentService _documents;
        private readonly IAuditLogService _audit;
        private readonly IFileStore _store;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public ProjectDocumentHandler(IProjectDocumentService documents, IAuditLogService audit, IFileStore store,
            IUserService users, IUserPermissionService permissions)
        {
            _documents = documents;
            _audit = audit;
            _store = store;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_users, _permissions);

        /// <summary>الوثيقة بإصداراتها، بعد التحقق من أن مشروعها ضمن حدّ المستخدم</summary>
        private async Task<ProjectDocument> LoadVisibleAsync(Viewer viewer, int id)
        {
            var document = await _documents.GetWithVersionsAsync(id) ?? throw new KeyNotFoundException("الوثيقة غير موجودة");
            ProjectRules.EnsureView(viewer, document.Project);
            return document;
        }

        public async Task<PagedResultDto<ProjectDocumentResponseDto>> Handle(SearchProjectDocumentsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            ProjectDocumentKind? kind = request.Kind is int k && ProjectRules.IsValidKind(k) ? (ProjectDocumentKind)k : null;

            var (items, total) = await _documents.SearchAsync(ProjectRules.Visible(viewer),
                new DocumentFilter(request.ProjectId, kind, request.Search, request.From, request.To), page, pageSize);
            return new PagedResultDto<ProjectDocumentResponseDto>
            {
                Items = items.Select(d => ToDto(d, viewer, withVersions: false)).ToList(),
                TotalCount = total, Page = page, PageSize = pageSize
            };
        }

        public async Task<ProjectDocumentResponseDto> Handle(GetProjectDocumentQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            return ToDto(await LoadVisibleAsync(viewer, request.Id), viewer, withVersions: true);
        }

        public async Task<ProjectDocumentResponseDto> Handle(ApproveProjectDocumentVersionCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var version = await _documents.GetVersionAsync(request.VersionId) ?? throw new KeyNotFoundException("الإصدار غير موجود");
            ProjectRules.EnsureView(viewer, version.Document.Project);
            ProjectRules.EnsureOpen(version.Document.Project);
            ProjectDocumentRules.EnsureCanApprove(viewer, version);

            await _documents.ApproveAsync(version.Id, viewer.Id, new AuditEntry(viewer.Id, AuditActions.Approved, AuditEntities.Version, version.Id,
                $"اعتماد الإصدار {version.VersionNumber} من «{version.Document.Title}»",
                $"الملف: {version.FileName}\nرفعه: {version.UploadedByUser?.FullName}\nSHA-256: {version.Sha256}"));
            return ToDto(await LoadVisibleAsync(viewer, version.DocumentId), viewer, withVersions: true);
        }

        public async Task<Unit> Handle(DeleteProjectDocumentCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var document = await LoadVisibleAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(document.Project);
            ProjectDocumentRules.EnsureCanDelete(document);

            var files = string.Join("، ", document.Versions.OrderBy(v => v.VersionNumber).Select(v => $"{v.VersionNumber}: {v.FileName}"));
            await _documents.DeleteDocumentAsync(document.Id, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Document, document.Id,
                $"حذف وثيقة «{document.Title}»", $"النوع: {ProjectRules.KindAr(document.Kind)}\nالإصدارات: {files}"));
            return Unit.Value;
        }

        public async Task<ProjectDocumentFileDto> Handle(GetProjectDocumentFileQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var version = await _documents.GetVersionAsync(request.VersionId) ?? throw new KeyNotFoundException("الإصدار غير موجود");
            var project = version.Document.Project;
            ProjectRules.EnsureView(viewer, project);

            var content = await StoredFiles.OpenAsync(_store, version.StorageKey, () => _documents.GetContentAsync(version.Id));
            if (ProjectDocumentRules.ShouldAuditDownload(version, request.FirstRequest))
                await _audit.WriteAsync(new AuditEntry(viewer.Id, AuditActions.Downloaded, AuditEntities.Version, version.Id,
                    $"فتح الإصدار المعتمد {version.VersionNumber} من «{version.Document.Title}»", $"الملف: {version.FileName}"),
                    project.Id, project.BranchId);

            // الصورة وPDF تُعرض داخل الصفحة ما لم يُطلب التنزيل؛ غيرها يُنزَّل دائماً
            var inline = !request.Download && MapFileRules.IsPreviewable(version.ContentType);
            return new ProjectDocumentFileDto(version.FileName, version.ContentType, content, inline);
        }

        // ───────── التحويل ─────────

        private static ProjectDocumentVersionResponseDto VersionDto(ProjectDocumentVersion v, Viewer viewer) => new()
        {
            Id = v.Id, VersionNumber = v.VersionNumber, FileName = v.FileName, ContentType = v.ContentType, Size = v.Size,
            Sha256 = v.Sha256, Source = v.Source, Note = v.Note,
            UploadedByUserId = v.UploadedByUserId, UploadedByName = v.UploadedByUser?.FullName ?? string.Empty, UploadedAt = v.UploadedAt,
            IsApproved = v.IsApproved, ApprovedByName = v.ApprovedByUser?.FullName, ApprovedAt = v.ApprovedAt,
            Previewable = MapFileRules.IsPreviewable(v.ContentType),
            CanApprove = viewer.Has(AppPermissions.ApproveProjectDocument) && !v.IsApproved && v.UploadedByUserId != viewer.Id
        };

        private static ProjectDocumentResponseDto ToDto(ProjectDocument d, Viewer viewer, bool withVersions)
        {
            var latest = ProjectDocumentRules.Latest(d);
            var approved = ProjectDocumentRules.Approved(d);
            return new()
            {
                Id = d.Id, ProjectId = d.ProjectId,
                ProjectNumber = d.Project == null ? string.Empty : ProjectRules.Number(d.Project.Id, d.Project.CreatedAt),
                ProjectName = d.Project?.Name ?? string.Empty,
                Title = d.Title, Kind = (int)d.Kind, KindName = ProjectRules.KindAr(d.Kind),
                CreatedByName = d.CreatedByUser?.FullName ?? string.Empty, CreatedAt = d.CreatedAt,
                VersionsCount = d.Versions.Count,
                Latest = latest == null ? null : VersionDto(latest, viewer),
                Approved = approved == null ? null : VersionDto(approved, viewer),
                Versions = withVersions ? d.Versions.OrderByDescending(v => v.VersionNumber).Select(v => VersionDto(v, viewer)).ToList() : null,
                CanDelete = viewer.Has(AppPermissions.DeleteProjectDocument) && approved == null
            };
        }
    }
}
