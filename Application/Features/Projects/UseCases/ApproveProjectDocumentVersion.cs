using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Projects.ProjectDocumentSupport;

namespace Application.Features.Projects
{
    public record ApproveProjectDocumentVersionCommand(int VersionId) : IRequest<ProjectDocumentResponseDto>;


    public class ApproveProjectDocumentVersionHandler(ProjectDocumentContext ctx) : IRequestHandler<ApproveProjectDocumentVersionCommand, ProjectDocumentResponseDto>
    {
        public async Task<ProjectDocumentResponseDto> Handle(ApproveProjectDocumentVersionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var version = await ctx.Documents.GetVersionAsync(request.VersionId) ?? throw new KeyNotFoundException("الإصدار غير موجود");
            ProjectRules.EnsureView(viewer, version.Document.Project);
            ProjectRules.EnsureOpen(version.Document.Project);
            ProjectDocumentRules.EnsureCanApprove(viewer, version);

            await ctx.Documents.ApproveAsync(version.Id, viewer.Id, new AuditEntry(viewer.Id, AuditActions.Approved, AuditEntities.Version, version.Id,
                $"اعتماد الإصدار {version.VersionNumber} من «{version.Document.Title}»",
                $"الملف: {version.FileName}\nرفعه: {version.UploadedByUser?.FullName}\nSHA-256: {version.Sha256}"));
            return ToDto(await ctx.LoadVisibleAsync(viewer, version.DocumentId), viewer, withVersions: true);
        }
    }
}
