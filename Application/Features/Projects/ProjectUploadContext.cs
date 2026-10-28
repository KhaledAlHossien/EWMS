using System.Security.Cryptography;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.Projects
{
    /// <summary>
    /// رفع وثائق المشروع على دفعات
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class ProjectUploadContext(IProjectUploadService uploads, IProjectUploadFiles files, IProjectService projects,
            IProjectDocumentService documents, IProjectFileSettings settings, IProjectUploadSignal signal,
            IUserService users, IUserPermissionService permissions)
    {
        public IProjectUploadService Uploads { get; } = uploads;
        public IProjectUploadFiles Files { get; } = files;
        public IProjectService Projects { get; } = projects;
        public IProjectDocumentService Documents { get; } = documents;
        public IProjectFileSettings Settings { get; } = settings;
        public IProjectUploadSignal Signal { get; } = signal;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>الجلسة المطلوبة وهي لصاحبها (غير الصاحب يُرفض ولو كان مدير النظام)</summary>
        public async Task<ProjectUpload> OwnedAsync(Viewer viewer, Guid id)
        {
            var upload = await Uploads.GetAsync(id) ?? throw new KeyNotFoundException("جلسة الرفع غير موجودة أو انتهت");
            if (upload.UserId != viewer.Id) throw new UnauthorizedAccessException("هذا الرفع ليس لك");
            return upload;
        }

        public async Task<Project> OpenVisibleProjectAsync(Viewer viewer, int projectId)
        {
            var project = await Projects.GetByIdAsync(projectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ProjectRules.EnsureView(viewer, project);
            ProjectRules.EnsureOpen(project);
            return project;
        }

        public ProjectUploadResponseDto ToDto(ProjectUpload u) => new()
        {
            Id = u.Id, ProjectId = u.ProjectId, DocumentId = u.DocumentId, Title = u.Title, Kind = (int)u.Kind,
            FileName = u.FileName, Size = u.Size, Received = u.Received, ChunkSize = Settings.ChunkBytes,
            Status = (int)u.Status, StatusName = ProjectUploadRules.StatusAr(u.Status), Error = u.Error,
            VersionId = u.VersionId, CreatedAt = u.CreatedAt, UpdatedAt = u.UpdatedAt
        };
    }
}
