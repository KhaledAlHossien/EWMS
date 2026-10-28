using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Projects.ProjectDocumentSupport;

namespace Application.Features.Projects
{
    public record GetProjectDocumentFileQuery(int VersionId, bool Download, bool FirstRequest) : IRequest<ProjectDocumentFileDto>;
    public record ProjectDocumentFileDto(string FileName, string ContentType, Stream Content, bool Inline);

    public class GetProjectDocumentFileHandler(ProjectDocumentContext ctx) : IRequestHandler<GetProjectDocumentFileQuery, ProjectDocumentFileDto>
    {
        public async Task<ProjectDocumentFileDto> Handle(GetProjectDocumentFileQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var version = await ctx.Documents.GetVersionAsync(request.VersionId) ?? throw new KeyNotFoundException("الإصدار غير موجود");
            var project = version.Document.Project;
            ProjectRules.EnsureView(viewer, project);

            var content = await StoredFiles.OpenAsync(ctx.Store, version.StorageKey, () => ctx.Documents.GetContentAsync(version.Id));
            if (ProjectDocumentRules.ShouldAuditDownload(version, request.FirstRequest))
                await ctx.Audit.WriteAsync(new AuditEntry(viewer.Id, AuditActions.Downloaded, AuditEntities.Version, version.Id,
                    $"فتح الإصدار المعتمد {version.VersionNumber} من «{version.Document.Title}»", $"الملف: {version.FileName}"),
                    project.Id, project.BranchId);

            // الصورة وPDF تُعرض داخل الصفحة ما لم يُطلب التنزيل؛ غيرها يُنزَّل دائماً
            var inline = !request.Download && MapFileRules.IsPreviewable(version.ContentType);
            return new ProjectDocumentFileDto(version.FileName, version.ContentType, content, inline);
        }
    }
}
