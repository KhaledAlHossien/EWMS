using Application.Common;
using Application.Features.Projects;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>حدود رؤية المشاريع: كل صلاحية حدّها ثابت، وإحالة المشروع لقسم لا تفتحه لكل مستخدميه (القاعدة 8)</summary>
public class ProjectRulesTests
{
    private const int BranchA = 1, BranchB = 2, DeptA1 = 10, DeptA2 = 11, DeptB1 = 20;

    private static User UserOf(int id, int? branch, int? dept, string role = "Emp") => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = dept
    };

    private static Viewer ViewerOf(User user, params string[] permissions) => new(user, new HashSet<string>(permissions));

    private static Project ProjectOf(int id, int branch, int? dept, int createdBy, params User[] members) => new()
    {
        Id = id, Name = "p" + id, BranchId = branch, DepartmentId = dept, CreatedByUserId = createdBy,
        Members = members.Select(m => new ProjectMember { ProjectId = id, UserId = m.Id, User = m }).ToList()
    };

    private static List<int> SeenBy(Viewer viewer, params Project[] projects)
    {
        var visible = ProjectRules.Visible(viewer).Compile();
        return projects.Where(visible).Select(p => p.Id).OrderBy(i => i).ToList();
    }

    [Fact]
    public void Nothing_is_visible_without_any_project_permission()
    {
        var me = UserOf(1, BranchA, DeptA1);
        var p = ProjectOf(1, BranchA, DeptA1, createdBy: 1, me);
        Assert.Empty(SeenBy(ViewerOf(me), p));
    }

    [Fact]
    public void Mine_shows_projects_where_I_am_a_member_or_the_creator()
    {
        var me = UserOf(1, BranchA, DeptA1);
        var other = UserOf(2, BranchA, DeptA1);
        var member = ProjectOf(1, BranchA, DeptA1, createdBy: 2, me, other);
        var created = ProjectOf(2, BranchA, DeptA1, createdBy: 1, other);   // أنشأتُه ثم أُزلت من مشاركيه
        var foreign = ProjectOf(3, BranchA, DeptA1, createdBy: 2, other);

        Assert.Equal([1, 2], SeenBy(ViewerOf(me, AppPermissions.ViewProjects), member, created, foreign));
    }

    [Fact]
    public void View_projects_alone_does_not_open_the_department_or_branch()
    {
        var me = UserOf(1, BranchA, DeptA1);
        var colleague = UserOf(2, BranchA, DeptA1);
        var sameDept = ProjectOf(1, BranchA, DeptA1, createdBy: 2, colleague);
        Assert.Empty(SeenBy(ViewerOf(me, AppPermissions.ViewProjects), sameDept));
    }

    [Fact]
    public void Creating_or_editing_projects_implies_seeing_my_own()
    {
        var me = UserOf(1, BranchA, DeptA1);
        var mine = ProjectOf(1, BranchA, DeptA1, createdBy: 1, me);
        Assert.Equal([1], SeenBy(ViewerOf(me, AppPermissions.CreateProject), mine));
        Assert.Equal([1], SeenBy(ViewerOf(me, AppPermissions.EditProject), mine));
    }

    [Fact]
    public void Department_scope_covers_projects_handled_by_my_department_or_with_a_member_from_it()
    {
        var me = UserOf(1, BranchA, DeptA1);
        var inDept = UserOf(2, BranchA, DeptA1);
        var inOther = UserOf(3, BranchA, DeptA2);

        var handledHere = ProjectOf(1, BranchA, DeptA1, createdBy: 3, inOther);
        var memberFromHere = ProjectOf(2, BranchA, DeptA2, createdBy: 3, inOther, inDept);
        var elsewhere = ProjectOf(3, BranchA, DeptA2, createdBy: 3, inOther);
        var otherBranch = ProjectOf(4, BranchB, DeptB1, createdBy: 3);

        Assert.Equal([1, 2], SeenBy(ViewerOf(me, AppPermissions.ViewDepartmentProjects), handledHere, memberFromHere, elsewhere, otherBranch));
    }

    [Fact]
    public void Handing_a_project_to_another_department_does_not_open_it_to_its_ordinary_users()
    {
        var holder = UserOf(1, BranchA, DeptA1);
        var newDeptUser = UserOf(2, BranchA, DeptA2);
        var project = ProjectOf(1, BranchA, DeptA2, createdBy: 1, holder);   // أُحيل إلى القسم A2

        // مستخدم القسم الجديد بصلاحية «مشاريعي» فقط لا يراه حتى يُضاف مشاركاً
        Assert.Empty(SeenBy(ViewerOf(newDeptUser, AppPermissions.ViewProjects), project));

        project.Members.Add(new ProjectMember { ProjectId = 1, UserId = 2, User = newDeptUser });
        Assert.Equal([1], SeenBy(ViewerOf(newDeptUser, AppPermissions.ViewProjects), project));
    }

    [Fact]
    public void Branch_scope_covers_my_branch_only()
    {
        var me = UserOf(1, BranchA, DeptA1);
        var a = ProjectOf(1, BranchA, DeptA2, createdBy: 9);
        var noDept = ProjectOf(2, BranchA, null, createdBy: 9);
        var b = ProjectOf(3, BranchB, DeptB1, createdBy: 9);
        Assert.Equal([1, 2], SeenBy(ViewerOf(me, AppPermissions.ViewBranchProjects), a, noDept, b));
    }

    [Fact]
    public void A_user_without_a_unit_matches_no_branch_or_department()
    {
        var me = UserOf(1, null, null);
        var p = ProjectOf(1, BranchA, null, createdBy: 9);
        var orphanDept = ProjectOf(2, BranchA, null, createdBy: 9, UserOf(3, BranchA, null));
        Assert.Empty(SeenBy(ViewerOf(me, AppPermissions.ViewBranchProjects, AppPermissions.ViewDepartmentProjects), p, orphanDept));
    }

    [Fact]
    public void Organization_wide_and_super_admin_see_everything()
    {
        var p1 = ProjectOf(1, BranchA, DeptA1, createdBy: 9);
        var p2 = ProjectOf(2, BranchB, DeptB1, createdBy: 9);
        Assert.Equal([1, 2], SeenBy(ViewerOf(UserOf(1, BranchA, DeptA1), AppPermissions.ViewOrganizationDashboard), p1, p2));
        Assert.Equal([1, 2], SeenBy(ViewerOf(UserOf(2, null, null, "SuperAdmin")), p1, p2));
    }

    [Fact]
    public void Scopes_combine_as_a_union()
    {
        var me = UserOf(1, BranchA, DeptA1);
        var inOther = UserOf(2, BranchA, DeptA2);
        var viaDepartment = ProjectOf(1, BranchA, DeptA1, createdBy: 2, inOther);
        var viaMembership = ProjectOf(2, BranchB, DeptB1, createdBy: 2, me);
        var neither = ProjectOf(3, BranchB, DeptB1, createdBy: 2, inOther);
        Assert.Equal([1, 2], SeenBy(ViewerOf(me, AppPermissions.ViewDepartmentProjects, AppPermissions.ViewProjects), viaDepartment, viaMembership, neither));
    }

    [Fact]
    public void EnsureView_refuses_outside_the_scope_with_unauthorized()
    {
        var me = UserOf(1, BranchA, DeptA1);
        var foreign = ProjectOf(1, BranchB, DeptB1, createdBy: 9);
        Assert.Throws<UnauthorizedAccessException>(() => ProjectRules.EnsureView(ViewerOf(me, AppPermissions.ViewBranchProjects), foreign));
    }

    [Fact]
    public void Projects_are_created_only_in_my_branch_unless_organization_wide()
    {
        var me = ViewerOf(UserOf(1, BranchA, DeptA1), AppPermissions.CreateProject);
        ProjectRules.EnsureCanCreateIn(me, BranchA);
        Assert.Throws<UnauthorizedAccessException>(() => ProjectRules.EnsureCanCreateIn(me, BranchB));

        var noBranch = ViewerOf(UserOf(2, null, null), AppPermissions.CreateProject);
        Assert.Throws<UnauthorizedAccessException>(() => ProjectRules.EnsureCanCreateIn(noBranch, BranchA));

        ProjectRules.EnsureCanCreateIn(ViewerOf(UserOf(3, null, null), AppPermissions.ViewOrganizationDashboard), BranchB);
    }

    [Fact]
    public void Closed_and_cancelled_projects_refuse_edits_until_reopened()
    {
        Assert.Throws<InvalidOperationException>(() => ProjectRules.EnsureOpen(new Project { Stage = ProjectStage.Closed }));
        Assert.Throws<InvalidOperationException>(() => ProjectRules.EnsureOpen(new Project { Stage = ProjectStage.Cancelled }));
        ProjectRules.EnsureOpen(new Project { Stage = ProjectStage.Warranty });
    }

    [Fact]
    public void Number_and_reference_are_clean()
    {
        Assert.Equal("PRJ-2026-00042", ProjectRules.Number(42, new DateTime(2026, 10, 27)));
        Assert.Null(ProjectRules.CleanReference("  "));
        Assert.Null(ProjectRules.CleanReference(null));
        Assert.Equal("ST-12/26", ProjectRules.CleanReference("  ST-12/26 "));
    }

    [Fact]
    public void Every_stage_and_document_kind_has_its_own_arabic_name()
    {
        foreach (var stage in Enum.GetValues<ProjectStage>())
            Assert.NotEqual(stage.ToString(), ProjectRules.StageAr(stage));

        var kinds = Enum.GetValues<ProjectDocumentKind>();
        // كل نوع غير «أخرى» له اسم خاص به (نوع جديد بلا اسم يقع على «أخرى» فيفشل هذا العدّ)
        Assert.Equal(kinds.Length - 1, kinds.Select(ProjectRules.KindAr).Where(n => n != "أخرى").Distinct().Count());
    }

    [Fact]
    public void Project_permissions_are_registered_once_with_their_any_of_policies()
    {
        var names = AppPermissions.All.Select(p => p.Name).ToList();
        var projectNames = names.Where(n => n.Contains("Project")).ToList();
        Assert.Equal(projectNames.Count, projectNames.Distinct().Count());

        string[] project =
        [
            AppPermissions.ViewProjects, AppPermissions.ViewDepartmentProjects, AppPermissions.ViewBranchProjects,
            AppPermissions.CreateProject, AppPermissions.EditProject, AppPermissions.ViewProjectDocuments,
            AppPermissions.DownloadProjectDocument, AppPermissions.UploadProjectDocument, AppPermissions.ApproveProjectDocument,
            AppPermissions.DeleteProjectDocument, AppPermissions.ViewProjectAudit, AppPermissions.ExportProjectsReport
        ];
        foreach (var name in project) Assert.Contains(name, names);
        foreach (var name in AppPermissions.AnyOf["AnyProjectView"]) Assert.Contains(name, names);
        Assert.Contains(AppPermissions.ExportProjectsReport, AppPermissions.AnyOf["AnyReportExport"]);
    }
}
