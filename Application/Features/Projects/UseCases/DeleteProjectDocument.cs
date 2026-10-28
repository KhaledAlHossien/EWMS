using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Projects.ProjectDocumentSupport;

namespace Application.Features.Projects
{
    public record DeleteProjectDocumentCommand(int Id) : IRequest<Unit>;


    public class DeleteProjectDocumentHandler(ProjectDocumentContext ctx) : IRequestHandler<DeleteProjectDocumentCommand, Unit>
    {
        public async Task<Unit> Handle(DeleteProjectDocumentCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var document = await ctx.LoadVisibleAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(document.Project);
            ProjectDocumentRules.EnsureCanDelete(document);

            var files = string.Join("، ", document.Versions.OrderBy(v => v.VersionNumber).Select(v => $"{v.VersionNumber}: {v.FileName}"));
            await ctx.Documents.DeleteDocumentAsync(document.Id, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Document, document.Id,
                $"حذف وثيقة «{document.Title}»", $"النوع: {ProjectRules.KindAr(document.Kind)}\nالإصدارات: {files}"));
            return Unit.Value;
        }
    }
}
