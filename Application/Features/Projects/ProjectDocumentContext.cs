using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Projects.ProjectDocumentSupport;

namespace Application.Features.Projects
{
    /// <summary>
    /// وثائق المشروع
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class ProjectDocumentContext(IProjectDocumentService documents, IAuditLogService audit, IFileStore store,
            IUserService users, IUserPermissionService permissions)
    {
        public IProjectDocumentService Documents { get; } = documents;
        public IAuditLogService Audit { get; } = audit;
        public IFileStore Store { get; } = store;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>الوثيقة بإصداراتها، بعد التحقق من أن مشروعها ضمن حدّ المستخدم</summary>
        public async Task<ProjectDocument> LoadVisibleAsync(Viewer viewer, int id)
        {
            var document = await Documents.GetWithVersionsAsync(id) ?? throw new KeyNotFoundException("الوثيقة غير موجودة");
            ProjectRules.EnsureView(viewer, document.Project);
            return document;
        }
    }
}
