using System.Linq.Expressions;
using Application.Common;
using Application.Features.AssignedTasks;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Projects.Tests;

public class ProjectMembersAndLinksTests
{
    private static User UserOf(int id, int? branch, int? dept, bool active = true) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1,
        Role = new Role { Id = 1, Name = "Emp" }, BranchId = branch, DepartmentId = dept, IsActive = active
    };

    private static Project ProjectIn(int branch, params int[] memberIds) => new()
    {
        Id = 5, Name = "p", BranchId = branch, CreatedAt = new DateTime(2026, 10, 27),
        Members = memberIds.Select(i => new ProjectMember { ProjectId = 5, UserId = i, User = UserOf(i, branch, 1) }).ToList()
    };

    [Fact]
    public void A_member_must_be_an_active_employee_of_the_project_branch()
    {
        var project = ProjectIn(branch: 1);
        ProjectRules.EnsureCanBeMember(project, UserOf(2, 1, 10));

        Assert.Throws<InvalidOperationException>(() => ProjectRules.EnsureCanBeMember(project, UserOf(3, 2, 20)));      // فرع آخر
        Assert.Throws<InvalidOperationException>(() => ProjectRules.EnsureCanBeMember(project, UserOf(4, null, null))); // بلا فرع
        Assert.Throws<InvalidOperationException>(() => ProjectRules.EnsureCanBeMember(project, UserOf(5, 1, 10, active: false)));
    }

    [Fact]
    public void A_member_cannot_be_added_twice_or_beyond_the_limit()
    {
        Assert.Throws<InvalidOperationException>(() => ProjectRules.EnsureCanBeMember(ProjectIn(1, 2), UserOf(2, 1, 10)));

        var full = ProjectIn(1, Enumerable.Range(100, ProjectRules.MaxMembers).ToArray());
        Assert.Throws<InvalidOperationException>(() => ProjectRules.EnsureCanBeMember(full, UserOf(2, 1, 10)));
    }

    // ───────── ربط المهام بمشروع: من لا يرى المشروع يعرف أن هناك رابطاً دون أي بيانات عنه ─────────

    private static TaskLinkResolver ResolverFor(Project? project) => new(null!, null!, null!, new StubProjects(project), null!, null!);

    [Fact]
    public async Task A_task_link_to_a_visible_project_shows_its_number_and_name()
    {
        var project = ProjectIn(1, 2);
        project.Name = "مبنى المحافظة";
        var viewer = new Viewer(UserOf(2, 1, 10), new HashSet<string> { AppPermissions.ViewProjects });

        var described = await ResolverFor(project).DescribeAsync(viewer, TaskLinkType.Project, 5);

        Assert.True(described.Exists);
        Assert.True(described.Available);
        Assert.Equal("PRJ-2026-00005 — مبنى المحافظة", described.Label);
    }

    [Fact]
    public async Task A_task_link_to_a_project_outside_my_scope_reveals_nothing()
    {
        var viewer = new Viewer(UserOf(9, 2, 20), new HashSet<string> { AppPermissions.ViewBranchProjects });

        var described = await ResolverFor(ProjectIn(1, 2)).DescribeAsync(viewer, TaskLinkType.Project, 5);

        Assert.True(described.Exists);
        Assert.False(described.Available);
        Assert.Equal(string.Empty, described.Label);
    }

    [Fact]
    public async Task A_task_link_to_a_missing_project_does_not_exist()
    {
        var viewer = new Viewer(UserOf(2, 1, 10), new HashSet<string> { AppPermissions.ViewOrganizationDashboard });
        var described = await ResolverFor(null).DescribeAsync(viewer, TaskLinkType.Project, 99);
        Assert.False(described.Exists);
    }

    [Fact]
    public void Project_is_a_known_task_link_type()
    {
        Assert.True(TaskLinkResolver.TryParseType("Project", out var type));
        Assert.Equal(TaskLinkType.Project, type);
        Assert.Equal("مشروع", TaskLinkResolver.TypeAr(type));
    }
}
