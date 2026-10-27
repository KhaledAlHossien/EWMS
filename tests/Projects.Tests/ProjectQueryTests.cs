using Application.Common;
using Application.Features.Projects;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Projects.Tests;

/// <summary>
/// شرط الرؤية يجب أن يُترجَم إلى SQL (لا يُقيَّم في الذاكرة): نبني الاستعلام دون اتصال بقاعدة بيانات ونقرأ نصه.
/// </summary>
public class ProjectQueryTests
{
    private static DataContext NewContext() =>
        new(new DbContextOptionsBuilder<DataContext>().UseSqlServer("Server=.;Database=EWMS_never_connected;Trusted_Connection=True").Options);

    private static Viewer ViewerOf(params string[] permissions) => new(
        new User
        {
            Id = 7, FullName = "u", Email = "u@x.sy", PasswordHash = "x", RoleId = 1,
            Role = new Role { Id = 1, Name = "Emp" }, BranchId = 1, DepartmentId = 10
        },
        new HashSet<string>(permissions));

    [Fact]
    public void Visible_translates_to_sql_with_branch_department_and_membership_conditions()
    {
        using var db = NewContext();
        var sql = db.Projects
            .Where(ProjectRules.Visible(ViewerOf(AppPermissions.ViewBranchProjects, AppPermissions.ViewDepartmentProjects, AppPermissions.ViewProjects)))
            .ToQueryString();

        Assert.Contains("[BranchId]", sql);
        Assert.Contains("[DepartmentId]", sql);
        Assert.Contains("[ProjectMembers]", sql);
        Assert.Contains("[CreatedByUserId]", sql);
    }

    [Fact]
    public void Audit_search_is_limited_to_visible_projects()
    {
        using var db = NewContext();
        var visibleIds = db.Projects.Where(ProjectRules.Visible(ViewerOf(AppPermissions.ViewProjects))).Select(p => p.Id);
        var sql = db.AuditLogs.Where(a => a.ProjectId != null && visibleIds.Contains(a.ProjectId.Value)).ToQueryString();

        Assert.Contains("[AuditLogs]", sql);
        Assert.Contains("[Projects]", sql);
    }
}

/// <summary>استعلامات الوثائق تُترجَم كاملة إلى SQL (بحث نصي، فلاتر، تضمين، ترتيب بآخر رفع، ترقيم) دون اتصال بقاعدة بيانات</summary>
public class ProjectDocumentQueryTests
{
    private static DataContext NewContext() =>
        new(new DbContextOptionsBuilder<DataContext>().UseSqlServer("Server=.;Database=EWMS_never_connected;Trusted_Connection=True").Options);

    private static Viewer ViewerOf() => new(
        new User { Id = 7, FullName = "u", Email = "u@x.sy", PasswordHash = "x", RoleId = 1, Role = new Role { Id = 1, Name = "Emp" }, BranchId = 1, DepartmentId = 10 },
        new HashSet<string> { AppPermissions.ViewBranchProjects });

    [Fact]
    public void The_search_limits_documents_to_visible_projects_and_applies_every_filter()
    {
        using var db = NewContext();
        var filter = new Application.Interfaces.DocumentFilter(5, ProjectDocumentKind.Contract, "PRJ-2026-00005", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        var filtered = Infrastructure.Persistence.Repositories.ProjectDocumentService.Filtered(db, ProjectRules.Visible(ViewerOf()), filter);
        var sql = filtered.ToQueryString();

        Assert.Contains("[ProjectDocuments]", sql);
        Assert.Contains("[Projects]", sql);          // الحدّ: مشاريع مرئية
        Assert.Contains("[ProjectDocumentVersions]", sql);   // فلتر التاريخ واسم الملف
        Assert.Contains("[Kind]", sql);
    }

    [Fact]
    public void The_page_query_includes_versions_orders_by_latest_upload_and_pages()
    {
        using var db = NewContext();
        var filtered = Infrastructure.Persistence.Repositories.ProjectDocumentService.Filtered(
            db, ProjectRules.Visible(ViewerOf()), new Application.Interfaces.DocumentFilter(null, null, null, null, null));

        var sql = Infrastructure.Persistence.Repositories.ProjectDocumentService.PageOf(filtered, 2, 20).ToQueryString();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("MAX(", sql, StringComparison.OrdinalIgnoreCase);
    }
}
