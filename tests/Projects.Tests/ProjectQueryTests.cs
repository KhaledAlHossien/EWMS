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
