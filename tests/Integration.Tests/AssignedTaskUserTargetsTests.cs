using System.Net;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// لوحة المهام: رئيس قسم أو فرع بلا مكتب يُسند لموظف مباشرة بصلاحيتي «موظفو قسمي» و«موظفو فرعي» (2026-10-28)،
/// ويفوّض مهمة قسمه لموظف في القسم دون المرور بمكتب. لكل صلاحية حدّها الثابت، وصلاحية «موظفو مكتبي» وحدها لا تكفي بلا مكتب.
/// </summary>
[Collection(ApiCollection.Name)]
public class AssignedTaskUserTargetsTests(ApiFixture api)
{
    private const string Base = "/api/AssignedTasks";

    private sealed record Org(ApiClient DepartmentHead, ApiClient OldStyleHead, ApiClient BranchHead, ApiClient Branchwide, int OfficeWorkerId, int OtherOfficeWorkerId,
        int DirectWorkerId, int OtherDepartmentWorkerId, int DepartmentId);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    private async Task<(ApiClient Client, int Id)> UserAsync(string label, int roleId, int? departmentId, int? officeId = null)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var fields = new List<KeyValuePair<string, string>>
        {
            new("FullName", "مهام " + label), new("Email", email), new("Password", ApiFixture.Password), new("RoleId", roleId.ToString()), new("BranchId", api.BranchId.ToString())
        };
        if (departmentId is int d) fields.Add(new("DepartmentId", d.ToString()));
        if (officeId is int o) fields.Add(new("OfficeId", o.ToString()));
        var user = (await api.Admin.PostForm<UserResponseDto>("/api/Users/Create", fields)).Ok();

        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return (api.WithToken(login.Token, user.Id), user.Id);
    }

    private async Task<int> OfficeAsync(string name, int departmentId) =>
        (await api.Admin.PostForm<OfficeResponseDto>("/api/Offices/Create", [new("Name", name + Guid.NewGuid().ToString("N")[..5]), new("Description", "اختبار"), new("DepartmentId", departmentId.ToString())])).Ok().Id;

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var s = Guid.NewGuid().ToString("N")[..6];
        var otherDepartment = (await api.Admin.PostForm<DepartmentResponseDto>("/api/Department/Create", [
            new("Name", "قسم آخر " + s), new("Description", "اختبار"), new("BranchId", api.BranchId.ToString())])).Ok();
        var office = await OfficeAsync("مكتب أ", api.DepartmentId);
        var otherOffice = await OfficeAsync("مكتب ب", api.DepartmentId);
        var foreignOffice = await OfficeAsync("مكتب خارجي", otherDepartment.Id);

        var departmentHeadRole = await api.CreateRoleAsync("رئيس قسم مهام " + s, ["ViewTaskBoard", "HandleUnitTasks", "AssignTaskToDepartmentUser", "AssignTaskToOffice"], all);
        var oldStyleRole = await api.CreateRoleAsync("رئيس قسم قديم " + s, ["ViewTaskBoard", "HandleUnitTasks", "AssignTaskToUser", "AssignTaskToOffice"], all);
        var branchHeadRole = await api.CreateRoleAsync("رئيس فرع مهام " + s, ["ViewTaskBoard", "AssignTaskToDepartment"], all);
        var branchwideRole = await api.CreateRoleAsync("مُسنِد فرعي " + s, ["ViewTaskBoard", "AssignTaskToBranchUser"], all);
        var workerRole = await api.CreateRoleAsync("موظف مهام " + s, ["ViewTaskBoard"], all);

        var departmentHead = await UserAsync("dhead", departmentHeadRole, api.DepartmentId);
        var oldStyle = await UserAsync("old", oldStyleRole, api.DepartmentId);
        var branchHead = await UserAsync("bhead", branchHeadRole, null);
        var branchwide = await UserAsync("bwide", branchwideRole, null);
        var inOffice = await UserAsync("wa", workerRole, api.DepartmentId, office);
        var inOtherOffice = await UserAsync("wb", workerRole, api.DepartmentId, otherOffice);
        var direct = await UserAsync("wd", workerRole, api.DepartmentId);
        var foreign = await UserAsync("wf", workerRole, otherDepartment.Id, foreignOffice);

        return new Org(departmentHead.Client, oldStyle.Client, branchHead.Client, branchwide.Client, inOffice.Id, inOtherOffice.Id, direct.Id, foreign.Id, api.DepartmentId);
    }

    private static object Task(string title, int targetId, string? targetType = null, int? parentTaskId = null) =>
        new { title, description = "اختبار", priority = 2, targetId, targetType, parentTaskId };

    [Fact]
    public async Task A_department_head_without_an_office_assigns_to_any_employee_of_the_department_but_not_outside_it()
    {
        var org = await OrgAsync();

        var board = (await org.DepartmentHead.Get<TaskBoardDto>($"{Base}/Board?mode=incoming")).Ok();
        Assert.Contains(board.TargetTypes, t => t.Value == "User");
        Assert.Contains(board.TargetTypes, t => t.Value == "Office");

        var options = (await org.DepartmentHead.Get<List<TaskTargetOptionDto>>($"{Base}/Targets?type=User")).Ok();
        Assert.Contains(options, o => o.Id == org.OfficeWorkerId);
        Assert.Contains(options, o => o.Id == org.OtherOfficeWorkerId);
        Assert.Contains(options, o => o.Id == org.DirectWorkerId);                  // يتبع للقسم مباشرة بلا مكتب
        Assert.DoesNotContain(options, o => o.Id == org.OtherDepartmentWorkerId);   // من قسم آخر

        var created = (await org.DepartmentHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("مهمة لموظف في مكتب", org.OtherOfficeWorkerId, "User"))).Ok();
        Assert.Equal("User", created.TargetType);
        var direct = (await org.DepartmentHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("مهمة لموظف بلا مكتب", org.DirectWorkerId, "User"))).Ok();
        Assert.Equal("User", direct.TargetType);

        Assert.Equal(HttpStatusCode.Forbidden, (await org.DepartmentHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("خارج القسم", org.OtherDepartmentWorkerId, "User"))).Status);
    }

    [Fact]
    public async Task The_office_permission_alone_does_not_open_employees_for_someone_without_an_office()
    {
        var org = await OrgAsync();
        var board = (await org.OldStyleHead.Get<TaskBoardDto>($"{Base}/Board?mode=incoming")).Ok();
        Assert.DoesNotContain(board.TargetTypes, t => t.Value == "User");
        Assert.Equal(HttpStatusCode.Forbidden, (await org.OldStyleHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("x", org.OfficeWorkerId, "User"))).Status);
    }

    [Fact]
    public async Task The_branchwide_permission_reaches_every_employee_of_the_branch()
    {
        var org = await OrgAsync();
        var options = (await org.Branchwide.Get<List<TaskTargetOptionDto>>($"{Base}/Targets?type=User")).Ok();
        Assert.Contains(options, o => o.Id == org.OfficeWorkerId);
        Assert.Contains(options, o => o.Id == org.OtherDepartmentWorkerId);
        Assert.All(options.Where(o => o.Id == org.OfficeWorkerId), o => Assert.False(string.IsNullOrEmpty(o.Detail)));

        (await org.Branchwide.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("مهمة عبر الأقسام", org.OtherDepartmentWorkerId, "User"))).Ok();
    }

    [Fact]
    public async Task A_department_task_is_delegated_straight_to_an_employee_of_the_department()
    {
        var org = await OrgAsync();
        var task = (await org.BranchHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("مهمة للقسم", org.DepartmentId, "Department"))).Ok();

        var asHead = (await org.DepartmentHead.Get<AssignedTaskDetailDto>($"{Base}/Get/{task.Id}")).Ok();
        Assert.True(asHead.CanDelegate);
        Assert.Equal(["Office", "User"], asHead.DelegationTypes.Select(t => t.Value).ToArray());
        var asOldStyle = (await org.OldStyleHead.Get<AssignedTaskDetailDto>($"{Base}/Get/{task.Id}")).Ok();
        Assert.Equal(["Office"], asOldStyle.DelegationTypes.Select(t => t.Value).ToArray());          // بلا صلاحية موظفي القسم: مكتب فقط

        // خيارات التفويض لكل نوع
        var users = (await org.DepartmentHead.Get<List<TaskTargetOptionDto>>($"{Base}/Targets?parentTaskId={task.Id}&type=User")).Ok();
        Assert.Contains(users, o => o.Id == org.DirectWorkerId);
        Assert.DoesNotContain(users, o => o.Id == org.OtherDepartmentWorkerId);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.OldStyleHead.Get<List<TaskTargetOptionDto>>($"{Base}/Targets?parentTaskId={task.Id}&type=User")).Status);

        // اختيار النوع إلزامي عند وجود أكثر من نوع، والتفويض لموظف من قسم المهمة فقط
        Assert.Equal(HttpStatusCode.BadRequest, (await org.DepartmentHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("بلا نوع", org.DirectWorkerId, null, task.Id))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.DepartmentHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("خارج القسم", org.OtherDepartmentWorkerId, "User", task.Id))).Status);
        var sub = (await org.DepartmentHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("تفويض لموظف", org.OfficeWorkerId, "User", task.Id))).Ok();
        Assert.Equal("User", sub.TargetType);
        Assert.Equal(task.Id, sub.ParentTaskId);

        // بلا صلاحية «موظفو القسم» لا تفويض مباشر لموظف
        Assert.Equal(HttpStatusCode.Forbidden, (await org.OldStyleHead.Post<AssignedTaskDetailDto>($"{Base}/Create", Task("ممنوع", org.OfficeWorkerId, "User", task.Id))).Status);
    }
}
