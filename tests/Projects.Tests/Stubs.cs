using System.Linq.Expressions;
using Application.Interfaces;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>بدائل خفيفة للخدمات في الاختبارات: ما لا يحتاجه الاختبار يرمي NotSupportedException كي لا يمرّ استدعاء غير مقصود صامتاً</summary>
internal sealed class StubProjects(Project? project) : IProjectService
{
    public Task<Project?> GetByIdAsync(int id) => Task.FromResult(project);
    public Task<(List<Project> Items, int TotalCount)> SearchAsync(Expression<Func<Project, bool>> visible, ProjectFilter filter, int page, int pageSize) => throw new NotSupportedException();
    public Task<Project> AddAsync(Project p, AuditEntry audit) => throw new NotSupportedException();
    public Task UpdateAsync(Project p, byte[]? rowVersion, AuditEntry audit) => throw new NotSupportedException();
    public Task AddMemberAsync(ProjectMember member, AuditEntry audit) => throw new NotSupportedException();
    public Task RemoveMemberAsync(ProjectMember member, AuditEntry audit) => throw new NotSupportedException();
    public Task<bool> ReferenceExistsAsync(int branchId, string reference, int? excludeProjectId = null) => throw new NotSupportedException();
    public Task<List<User>> GetMemberCandidatesAsync(int branchId, string? search, IReadOnlyCollection<int> excludeUserIds, int take) => throw new NotSupportedException();
    public Task<bool> ExistsForBranchAsync(int branchId) => throw new NotSupportedException();
    public Task<bool> ExistsForDepartmentAsync(int departmentId) => throw new NotSupportedException();
    public Task<bool> ExistsForSiteAsync(int siteId) => throw new NotSupportedException();
    public Task<bool> ExistsForUserAsync(int userId) => throw new NotSupportedException();
}

internal sealed class StubUploads : IProjectUploadService
{
    public readonly Dictionary<Guid, ProjectUpload> Items = new();

    public Task<ProjectUpload?> GetAsync(Guid id) => Task.FromResult(Items.GetValueOrDefault(id));
    public Task<List<ProjectUpload>> GetMineAsync(int userId, int? projectId) => throw new NotSupportedException();
    public Task<int> CountOpenAsync(int userId) => throw new NotSupportedException();
    public Task AddAsync(ProjectUpload upload) { Items[upload.Id] = upload; return Task.CompletedTask; }
    public Task SetReceivedAsync(Guid id, long received) { Items[id].Received = received; return Task.CompletedTask; }
    public Task SetStatusAsync(Guid id, ProjectUploadStatus status, string error = "") { Items[id].Status = status; Items[id].Error = error; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id) { Items.Remove(id); return Task.CompletedTask; }
    public Task<List<Guid>> GetProcessingIdsAsync() => throw new NotSupportedException();
    public Task<List<Guid>> GetExpiredIdsAsync(DateTime receivingBefore, DateTime finishedBefore) => throw new NotSupportedException();
    public Task<int> FailStuckAsync(DateTime before) => throw new NotSupportedException();
}

internal sealed class StubDocuments : IProjectDocumentService
{
    public NewVersionData? Added;
    public byte[]? AddedContent;

    public Task<ProjectDocument?> GetDocumentAsync(int id) => Task.FromResult<ProjectDocument?>(new ProjectDocument { Id = id, ProjectId = 5 });
    public Task<(List<ProjectDocument> Items, int TotalCount)> SearchAsync(System.Linq.Expressions.Expression<Func<Project, bool>> visibleProjects, DocumentFilter filter, int page, int pageSize) => throw new NotSupportedException();
    public Task<ProjectDocument?> GetWithVersionsAsync(int id) => throw new NotSupportedException();
    public Task<ProjectDocumentVersion?> GetVersionAsync(int versionId) => throw new NotSupportedException();
    public Task<byte[]?> GetContentAsync(int versionId) => throw new NotSupportedException();
    public Task ApproveAsync(int versionId, int userId, AuditEntry audit) => throw new NotSupportedException();
    public Task DeleteDocumentAsync(int documentId, AuditEntry audit) => throw new NotSupportedException();

    public Task<ProjectDocumentVersion> AddVersionAsync(Guid uploadId, NewVersionData data, byte[]? content)
    {
        Added = data;
        AddedContent = content;
        return Task.FromResult(new ProjectDocumentVersion { Id = 1, VersionNumber = 1, FileName = data.FileName });
    }
}
