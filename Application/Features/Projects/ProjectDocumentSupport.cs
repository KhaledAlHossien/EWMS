using Application.Common;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Projects
{
    /// <summary>دوال ساكنة مشتركة بين معالجات وثائق المشروع: تحويل إلى DTO وتنسيق وحراسات صرفة.</summary>
    public static class ProjectDocumentSupport
    {
        public static ProjectDocumentVersionResponseDto VersionDto(ProjectDocumentVersion v, Viewer viewer) => new()
        {
            Id = v.Id, VersionNumber = v.VersionNumber, FileName = v.FileName, ContentType = v.ContentType, Size = v.Size,
            Sha256 = v.Sha256, Source = v.Source, Note = v.Note,
            UploadedByUserId = v.UploadedByUserId, UploadedByName = v.UploadedByUser?.FullName ?? string.Empty, UploadedAt = v.UploadedAt,
            IsApproved = v.IsApproved, ApprovedByName = v.ApprovedByUser?.FullName, ApprovedAt = v.ApprovedAt,
            Previewable = MapFileRules.IsPreviewable(v.ContentType),
            CanApprove = viewer.Has(AppPermissions.ApproveProjectDocument) && !v.IsApproved && v.UploadedByUserId != viewer.Id
        };

        public static ProjectDocumentResponseDto ToDto(ProjectDocument d, Viewer viewer, bool withVersions)
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
