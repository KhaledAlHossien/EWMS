using Application.Common;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using NSubstitute;

namespace Projects.Tests;

public class ProjectDocumentRulesTests
{
    private static ProjectDocument DocWith(params (int Number, bool Approved)[] versions) => new()
    {
        Id = 1, Versions = versions.Select(v => new ProjectDocumentVersion { VersionNumber = v.Number, IsApproved = v.Approved }).ToList()
    };

    [Fact]
    public void Latest_is_the_highest_number_and_approved_is_the_newest_approved()
    {
        var doc = DocWith((1, true), (2, false), (3, true), (4, false));
        Assert.Equal(4, ProjectDocumentRules.Latest(doc)!.VersionNumber);
        Assert.Equal(3, ProjectDocumentRules.Approved(doc)!.VersionNumber);
        Assert.Null(ProjectDocumentRules.Approved(DocWith((1, false))));
        Assert.Null(ProjectDocumentRules.Latest(DocWith()));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("bytes=0-99", true)]
    [InlineData("bytes=0-", true)]
    [InlineData("bytes=-500", true)]          // آخر 500 بايت: يُعدّ طلباً أول (الأحوط تسجيلاً)
    [InlineData("bytes=1048576-", false)]
    [InlineData("bytes=100-199", false)]
    [InlineData("items=5-9", true)]           // غير مفهوم
    public void The_first_request_is_the_one_without_a_range_or_starting_at_zero(string? header, bool expected) =>
        Assert.Equal(expected, ProjectDocumentRules.IsFirstRequest(header));

    [Fact]
    public void Only_the_first_request_of_an_approved_version_is_audited()
    {
        var approved = new ProjectDocumentVersion { IsApproved = true };
        var draft = new ProjectDocumentVersion();
        Assert.True(ProjectDocumentRules.ShouldAuditDownload(approved, firstRequest: true));
        Assert.False(ProjectDocumentRules.ShouldAuditDownload(approved, firstRequest: false));
        Assert.False(ProjectDocumentRules.ShouldAuditDownload(draft, firstRequest: true));
    }

    [Fact]
    public void An_uploader_never_approves_his_own_version()
    {
        var uploader = new Viewer(new User { Id = 7, FullName = "u", Email = "u@x.sy", PasswordHash = "x", RoleId = 1, Role = new Role { Name = "SuperAdmin" } },
            new HashSet<string>());
        Assert.Throws<InvalidOperationException>(() =>
            ProjectDocumentRules.EnsureCanApprove(uploader, new ProjectDocumentVersion { UploadedByUserId = 7 }));
        ProjectDocumentRules.EnsureCanApprove(uploader, new ProjectDocumentVersion { UploadedByUserId = 8 });
        Assert.Throws<InvalidOperationException>(() =>
            ProjectDocumentRules.EnsureCanApprove(uploader, new ProjectDocumentVersion { UploadedByUserId = 8, IsApproved = true }));
    }

    [Fact]
    public void A_document_with_an_approved_version_cannot_be_deleted()
    {
        Assert.Throws<InvalidOperationException>(() => ProjectDocumentRules.EnsureCanDelete(DocWith((1, true), (2, false))));
        ProjectDocumentRules.EnsureCanDelete(DocWith((1, false)));
    }
}

/// <summary>معالج الوثائق بحدود الرؤية والصلاحيات: ما يخرج عن حدّ المشروع لا يُقرأ ولا يُعتمد ولا يُحذف ولا يُفتح</summary>
public class ProjectDocumentHandlerTests
{
    private const int Me = 2, Uploader = 3;

    private sealed class Fixture
    {
        public IProjectDocumentService Documents = Substitute.For<IProjectDocumentService>();
        public IAuditLogService Audit = Substitute.For<IAuditLogService>();
        public IFileStore Store = Substitute.For<IFileStore>();
        public ProjectDocumentHandler Handler = null!;
    }

    private static User UserOf(int id, int? branch = 1, int? dept = 10) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1,
        Role = new Role { Id = 1, Name = "Emp" }, BranchId = branch, DepartmentId = dept
    };

    private static Fixture Create(User me, params string[] permissions)
    {
        var users = Substitute.For<IUserService>();
        users.UserId.Returns(me.Id);
        users.GetByIdAsync(me.Id).Returns(me);
        var perms = Substitute.For<IUserPermissionService>();
        perms.GetAsync(me.Id).Returns((IReadOnlySet<string>)new HashSet<string>(permissions));

        var f = new Fixture();
        f.Handler = new ProjectDocumentHandler(f.Documents, f.Audit, f.Store, users, perms);
        return f;
    }

    private static Project ProjectOf(int branch, ProjectStage stage = ProjectStage.Execution, params int[] memberIds) => new()
    {
        Id = 5, Name = "مشروع", BranchId = branch, Stage = stage, CreatedAt = new DateTime(2026, 10, 27),
        Members = memberIds.Select(i => new ProjectMember { ProjectId = 5, UserId = i, User = UserOf(i) }).ToList()
    };

    private static ProjectDocument DocumentOf(Project project, bool approved = false, string contentType = "application/pdf")
    {
        var document = new ProjectDocument { Id = 9, ProjectId = project.Id, Project = project, Title = "مخطط", Kind = ProjectDocumentKind.StructuralDrawing };
        document.Versions.Add(new ProjectDocumentVersion
        {
            Id = 40, DocumentId = 9, Document = document, VersionNumber = 1, FileName = "plan.pdf", ContentType = contentType,
            Size = 3, Sha256 = "ab", UploadedByUserId = Uploader, UploadedByUser = UserOf(Uploader), UploadedAt = DateTime.UtcNow,
            IsApproved = approved, StorageKey = null
        });
        return document;
    }

    // ───────── الحدّ ─────────

    [Fact]
    public async Task A_document_of_a_project_outside_my_scope_is_not_readable()
    {
        var f = Create(UserOf(Me), AppPermissions.ViewProjectDocuments, AppPermissions.ViewProjects);
        f.Documents.GetWithVersionsAsync(9).Returns(DocumentOf(ProjectOf(branch: 1)));   // لست مشاركاً فيه

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Handler.Handle(new GetProjectDocumentQuery(9), default));
    }

    [Fact]
    public async Task A_member_reads_the_document_with_its_versions()
    {
        var f = Create(UserOf(Me), AppPermissions.ViewProjectDocuments, AppPermissions.ViewProjects);
        f.Documents.GetWithVersionsAsync(9).Returns(DocumentOf(ProjectOf(1, memberIds: Me)));

        var dto = await f.Handler.Handle(new GetProjectDocumentQuery(9), default);

        Assert.Equal("PRJ-2026-00005", dto.ProjectNumber);
        Assert.Single(dto.Versions!);
        Assert.Equal("plan.pdf", dto.Latest!.FileName);
    }

    [Fact]
    public async Task A_file_of_a_project_outside_my_scope_does_not_open()
    {
        var f = Create(UserOf(Me), AppPermissions.DownloadProjectDocument, AppPermissions.ViewProjects);
        f.Documents.GetVersionAsync(40).Returns(DocumentOf(ProjectOf(1)).Versions.First());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Handler.Handle(new GetProjectDocumentFileQuery(40, false, true), default));
        await f.Documents.DidNotReceive().GetContentAsync(Arg.Any<int>());
    }

    // ───────── الاعتماد ─────────

    [Fact]
    public async Task Approving_my_own_upload_is_refused_and_nothing_is_saved()
    {
        var f = Create(UserOf(Uploader), AppPermissions.ApproveProjectDocument, AppPermissions.ViewProjects);
        f.Documents.GetVersionAsync(40).Returns(DocumentOf(ProjectOf(1, memberIds: Uploader)).Versions.First());

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new ApproveProjectDocumentVersionCommand(40), default));
        await f.Documents.DidNotReceive().ApproveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<AuditEntry>());
    }

    [Fact]
    public async Task A_second_member_approves_and_the_audit_entry_names_the_action()
    {
        var f = Create(UserOf(Me), AppPermissions.ApproveProjectDocument, AppPermissions.ViewProjects);
        var document = DocumentOf(ProjectOf(1, memberIds: Me));
        f.Documents.GetVersionAsync(40).Returns(document.Versions.First());
        f.Documents.GetWithVersionsAsync(9).Returns(document);

        await f.Handler.Handle(new ApproveProjectDocumentVersionCommand(40), default);

        await f.Documents.Received(1).ApproveAsync(40, Me,
            Arg.Is<AuditEntry>(a => a.Action == AuditActions.Approved && a.EntityType == AuditEntities.Version && a.UserId == Me));
    }

    [Fact]
    public async Task Approving_in_a_closed_project_is_refused()
    {
        var f = Create(UserOf(Me), AppPermissions.ApproveProjectDocument, AppPermissions.ViewProjects);
        f.Documents.GetVersionAsync(40).Returns(DocumentOf(ProjectOf(1, ProjectStage.Closed, Me)).Versions.First());

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new ApproveProjectDocumentVersionCommand(40), default));
        await f.Documents.DidNotReceive().ApproveAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<AuditEntry>());
    }

    // ───────── الحذف ─────────

    [Fact]
    public async Task Deleting_a_document_with_an_approved_version_is_refused()
    {
        var f = Create(UserOf(Me), AppPermissions.DeleteProjectDocument, AppPermissions.ViewProjects);
        f.Documents.GetWithVersionsAsync(9).Returns(DocumentOf(ProjectOf(1, memberIds: Me), approved: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new DeleteProjectDocumentCommand(9), default));
        await f.Documents.DidNotReceive().DeleteDocumentAsync(Arg.Any<int>(), Arg.Any<AuditEntry>());
    }

    [Fact]
    public async Task Deleting_a_draft_document_is_audited()
    {
        var f = Create(UserOf(Me), AppPermissions.DeleteProjectDocument, AppPermissions.ViewProjects);
        f.Documents.GetWithVersionsAsync(9).Returns(DocumentOf(ProjectOf(1, memberIds: Me)));

        await f.Handler.Handle(new DeleteProjectDocumentCommand(9), default);

        await f.Documents.Received(1).DeleteDocumentAsync(9, Arg.Is<AuditEntry>(a => a.Action == AuditActions.Deleted));
    }

    // ───────── فتح الملف ─────────

    private static Fixture FileFixture(bool approved, string contentType = "application/pdf")
    {
        var f = Create(UserOf(Me), AppPermissions.DownloadProjectDocument, AppPermissions.ViewProjects);
        f.Documents.GetVersionAsync(40).Returns(DocumentOf(ProjectOf(1, memberIds: Me), approved, contentType).Versions.First());
        f.Documents.GetContentAsync(40).Returns([1, 2, 3]);
        return f;
    }

    [Fact]
    public async Task Opening_an_approved_version_is_audited_once_not_per_range_request()
    {
        var f = FileFixture(approved: true);

        await f.Handler.Handle(new GetProjectDocumentFileQuery(40, Download: false, FirstRequest: true), default);
        await f.Handler.Handle(new GetProjectDocumentFileQuery(40, Download: false, FirstRequest: false), default);

        await f.Audit.Received(1).WriteAsync(Arg.Is<AuditEntry>(a => a.Action == AuditActions.Downloaded), 5, 1);
    }

    [Fact]
    public async Task Opening_an_unapproved_version_is_not_audited()
    {
        var f = FileFixture(approved: false);
        await f.Handler.Handle(new GetProjectDocumentFileQuery(40, false, true), default);
        await f.Audit.DidNotReceive().WriteAsync(Arg.Any<AuditEntry>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Pdf_opens_inline_unless_download_is_requested_and_other_types_always_download()
    {
        var pdf = FileFixture(false);
        Assert.True((await pdf.Handler.Handle(new GetProjectDocumentFileQuery(40, false, true), default)).Inline);
        Assert.False((await pdf.Handler.Handle(new GetProjectDocumentFileQuery(40, true, true), default)).Inline);

        var zip = FileFixture(false, "application/zip");
        Assert.False((await zip.Handler.Handle(new GetProjectDocumentFileQuery(40, false, true), default)).Inline);
    }

    [Fact]
    public async Task A_version_stored_in_the_folder_is_read_from_the_store()
    {
        var f = Create(UserOf(Me), AppPermissions.DownloadProjectDocument, AppPermissions.ViewProjects);
        var version = DocumentOf(ProjectOf(1, memberIds: Me)).Versions.First();
        version.StorageKey = "projects/2026/10/" + new string('a', 32);
        f.Documents.GetVersionAsync(40).Returns(version);
        f.Store.OpenRead(version.StorageKey).Returns(new MemoryStream([9, 9]));

        var file = await f.Handler.Handle(new GetProjectDocumentFileQuery(40, false, true), default);

        Assert.Equal(2, file.Content.Length);
        await f.Documents.DidNotReceive().GetContentAsync(Arg.Any<int>());
    }
}
