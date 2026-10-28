using Application.Common;
using Application.Features.AssignedTasks;
using Domain.Entities;
using Domain.Enums;

namespace Projects.Tests.Regression;

/// <summary>
/// آلة حالات لوحة المهام (قرارات 2026-10-07 و10-10): المنفِّذ ينفّذ ويرسل للمراجعة، المُسنِد وحده يعتمد أو يعيد،
/// «بانتظار المراجعة» تجمّد المهمة لغير المُراجِع، و«تم التنفيذ» نهائية.
/// </summary>
public class AssignedTaskRulesTests
{
    private const int Creator = 1, Handler = 2, Stranger = 3;
    private const int Branch = 1, Department = 10, Office = 100;

    private static User UserOf(int id, int? branch = Branch, int? department = Department, int? office = Office, string role = "Emp") => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = department, OfficeId = office
    };

    private static Viewer ViewerOf(User user, params string[] permissions) => new(user, new HashSet<string>(permissions));

    private static AssignedTask UserTask(AssignedTaskStatus status) => new()
    {
        Id = 1, Title = "مهمة", Status = status, TargetType = AssignedTaskTargetType.User,
        BranchId = Branch, DepartmentId = Department, OfficeId = Office, AssigneeUserId = Handler, CreatedByUserId = Creator
    };

    private static Viewer AsHandler() => ViewerOf(UserOf(Handler));
    private static Viewer AsCreator() => ViewerOf(UserOf(Creator, office: null));
    private static Viewer AsStranger() => ViewerOf(UserOf(Stranger, branch: 2, department: 20, office: 200));

    private static AssignedTaskStatus[] Allowed(AssignedTaskStatus status, Viewer viewer) =>
        AssignedTaskRules.AllowedStatuses(UserTask(status), viewer).ToArray();

    // ───────── الانتقالات المسموحة ─────────

    [Fact]
    public void The_handler_starts_a_task_and_can_put_it_back()
    {
        Assert.Equal([AssignedTaskStatus.InProgress], Allowed(AssignedTaskStatus.Todo, AsHandler()));
        Assert.Equal([AssignedTaskStatus.Todo, AssignedTaskStatus.InReview], Allowed(AssignedTaskStatus.InProgress, AsHandler()));
    }

    [Fact]
    public void The_handler_can_never_mark_a_task_done_and_cannot_pull_it_back_from_review()
    {
        Assert.DoesNotContain(AssignedTaskStatus.Done, Allowed(AssignedTaskStatus.InProgress, AsHandler()));
        Assert.Empty(Allowed(AssignedTaskStatus.InReview, AsHandler()));
    }

    [Fact]
    public void The_creator_approves_or_returns_a_task_in_review()
    {
        Assert.Equal([AssignedTaskStatus.InProgress, AssignedTaskStatus.Done], Allowed(AssignedTaskStatus.InReview, AsCreator()));
    }

    [Fact]
    public void The_creator_can_finish_a_task_in_progress_directly_but_not_start_it()
    {
        Assert.Equal([AssignedTaskStatus.Done], Allowed(AssignedTaskStatus.InProgress, AsCreator()));
        Assert.Empty(Allowed(AssignedTaskStatus.Todo, AsCreator()));
    }

    [Fact]
    public void Done_is_final_for_everyone_including_the_super_admin()
    {
        var admin = ViewerOf(UserOf(9, role: "SuperAdmin"));

        Assert.Empty(Allowed(AssignedTaskStatus.Done, AsHandler()));
        Assert.Empty(Allowed(AssignedTaskStatus.Done, AsCreator()));
        Assert.Empty(Allowed(AssignedTaskStatus.Done, admin));
    }

    [Theory]
    [InlineData(AssignedTaskStatus.Todo)]
    [InlineData(AssignedTaskStatus.InProgress)]
    [InlineData(AssignedTaskStatus.InReview)]
    public void A_stranger_moves_nothing(AssignedTaskStatus status)
    {
        Assert.Empty(Allowed(status, AsStranger()));
    }

    [Fact]
    public void Allowed_targets_list_done_last()
    {
        var all = AssignedTaskRules.AllowedStatuses(UserTask(AssignedTaskStatus.InReview), AsCreator());
        Assert.Equal(AssignedTaskStatus.Done, all.Last());
    }

    // ───────── التجميد ─────────

    [Fact]
    public void A_task_in_review_is_frozen_for_everyone_but_the_reviewer()
    {
        var task = UserTask(AssignedTaskStatus.InReview);

        Assert.True(AssignedTaskRules.IsFrozenFor(task, AsHandler()));
        Assert.True(AssignedTaskRules.IsFrozenFor(task, AsStranger()));
        Assert.False(AssignedTaskRules.IsFrozenFor(task, AsCreator()));
        Assert.False(AssignedTaskRules.IsFrozenFor(task, ViewerOf(UserOf(9, role: "SuperAdmin"))));
        Assert.Throws<InvalidOperationException>(() => AssignedTaskRules.EnsureNotFrozen(task, AsHandler()));
        AssignedTaskRules.EnsureNotFrozen(task, AsCreator());
    }

    [Theory]
    [InlineData(AssignedTaskStatus.Todo)]
    [InlineData(AssignedTaskStatus.InProgress)]
    [InlineData(AssignedTaskStatus.Done)]
    public void A_task_outside_review_is_never_frozen(AssignedTaskStatus status)
    {
        Assert.False(AssignedTaskRules.IsFrozenFor(UserTask(status), AsHandler()));
    }

    [Fact]
    public void Returning_from_review_needs_a_note_only_when_the_creator_is_not_the_handler()
    {
        var review = UserTask(AssignedTaskStatus.InReview);

        Assert.True(AssignedTaskRules.NeedsReturnNote(review, AsCreator()));
        Assert.False(AssignedTaskRules.NeedsReturnNote(review, AsHandler()));
        Assert.False(AssignedTaskRules.NeedsReturnNote(UserTask(AssignedTaskStatus.InProgress), AsCreator()));
    }

    // ───────── من ينفّذ ─────────

    [Fact]
    public void A_user_task_is_handled_by_its_assignee_without_any_permission()
    {
        Assert.True(AssignedTaskRules.CanHandle(UserTask(AssignedTaskStatus.Todo), AsHandler()));
        Assert.False(AssignedTaskRules.CanHandle(UserTask(AssignedTaskStatus.Todo), AsStranger()));
    }

    [Fact]
    public void A_department_task_is_handled_only_by_holders_directly_in_that_department()
    {
        var task = new AssignedTask { Id = 2, Title = "ت", TargetType = AssignedTaskTargetType.Department, BranchId = Branch, DepartmentId = Department, CreatedByUserId = Creator };

        var direct = ViewerOf(UserOf(5, office: null), AppPermissions.HandleUnitTasks);
        var inOffice = ViewerOf(UserOf(6), AppPermissions.HandleUnitTasks);
        var withoutPermission = ViewerOf(UserOf(7, office: null));

        Assert.True(AssignedTaskRules.CanHandle(task, direct));
        Assert.False(AssignedTaskRules.CanHandle(task, inOffice));
        Assert.False(AssignedTaskRules.CanHandle(task, withoutPermission));
    }

    [Fact]
    public void An_office_task_is_handled_only_by_holders_in_that_office()
    {
        var task = new AssignedTask { Id = 3, Title = "ت", TargetType = AssignedTaskTargetType.Office, BranchId = Branch, DepartmentId = Department, OfficeId = Office, CreatedByUserId = Creator };

        Assert.True(AssignedTaskRules.CanHandle(task, ViewerOf(UserOf(5), AppPermissions.HandleUnitTasks)));
        Assert.False(AssignedTaskRules.CanHandle(task, ViewerOf(UserOf(6, office: 101), AppPermissions.HandleUnitTasks)));
        Assert.False(AssignedTaskRules.CanHandle(task, ViewerOf(UserOf(7))));
    }

    [Fact]
    public void Delegation_goes_exactly_one_level_down()
    {
        Assert.Equal(AssignedTaskTargetType.Office, AssignedTaskRules.DelegationTargetFor(new AssignedTask { Title = "ت", TargetType = AssignedTaskTargetType.Department }));
        Assert.Equal(AssignedTaskTargetType.User, AssignedTaskRules.DelegationTargetFor(new AssignedTask { Title = "ت", TargetType = AssignedTaskTargetType.Office }));
        Assert.Null(AssignedTaskRules.DelegationTargetFor(new AssignedTask { Title = "ت", TargetType = AssignedTaskTargetType.User }));
    }

    // ───────── الإسناد والرؤية ─────────

    [Fact]
    public void Assignment_targets_depend_on_the_permissions_and_the_users_placement()
    {
        var all = ViewerOf(UserOf(Creator), AppPermissions.AssignTaskToDepartment, AppPermissions.AssignTaskToOffice, AppPermissions.AssignTaskToUser);
        Assert.Equal([AssignedTaskTargetType.Department, AssignedTaskTargetType.Office, AssignedTaskTargetType.User], AssignedTaskRules.TargetTypesFor(all));

        // بلا مكتب لا إسناد لموظفي مكتب، وبلا قسم لا إسناد لمكاتب قسم
        var noOffice = ViewerOf(UserOf(Creator, office: null), AppPermissions.AssignTaskToOffice, AppPermissions.AssignTaskToUser);
        Assert.Equal([AssignedTaskTargetType.Office], AssignedTaskRules.TargetTypesFor(noOffice));

        var noUnit = ViewerOf(UserOf(Creator, branch: null, department: null, office: null), AppPermissions.AssignTaskToDepartment, AppPermissions.AssignTaskToOffice);
        Assert.Empty(AssignedTaskRules.TargetTypesFor(noUnit));

        Assert.Empty(AssignedTaskRules.TargetTypesFor(ViewerOf(UserOf(Creator))));
    }

    [Fact]
    public void The_creator_and_the_handler_see_the_task_and_a_stranger_does_not()
    {
        var task = UserTask(AssignedTaskStatus.Todo);

        Assert.True(AssignedTaskRules.CanView(task, AsCreator()));
        Assert.True(AssignedTaskRules.CanView(task, AsHandler()));
        Assert.False(AssignedTaskRules.CanView(task, AsStranger()));
    }

    [Fact]
    public void A_holder_of_an_assign_permission_sees_the_tasks_of_the_unit_it_covers_and_no_other()
    {
        var task = UserTask(AssignedTaskStatus.Todo);

        Assert.True(AssignedTaskRules.CanView(task, ViewerOf(UserOf(8, office: 101), AppPermissions.AssignTaskToDepartment)), "رئيس فرع المهمة");
        Assert.False(AssignedTaskRules.CanView(task, ViewerOf(UserOf(8, branch: 2, department: 20, office: 200), AppPermissions.AssignTaskToDepartment)), "فرع آخر");
        Assert.True(AssignedTaskRules.CanView(task, ViewerOf(UserOf(8, office: 101), AppPermissions.AssignTaskToOffice)), "قسم المهمة");
        Assert.False(AssignedTaskRules.CanView(task, ViewerOf(UserOf(8, department: 11, office: 101), AppPermissions.AssignTaskToOffice)), "قسم آخر");
    }

    [Fact]
    public void The_super_admin_sees_every_task()
    {
        Assert.True(AssignedTaskRules.CanView(UserTask(AssignedTaskStatus.Todo), ViewerOf(UserOf(9, branch: null, department: null, office: null, role: "SuperAdmin"))));
    }

    [Fact]
    public void The_scope_tab_exists_only_for_assigners_handlers_and_the_admin()
    {
        Assert.False(AssignedTaskRules.HasScope(ViewerOf(UserOf(8))));
        Assert.True(AssignedTaskRules.HasScope(ViewerOf(UserOf(8), AppPermissions.HandleUnitTasks)));
        Assert.True(AssignedTaskRules.HasScope(ViewerOf(UserOf(8), AppPermissions.AssignTaskToUser)));
        Assert.True(AssignedTaskRules.HasScope(ViewerOf(UserOf(9, role: "SuperAdmin"))));
    }

    [Fact]
    public void The_incoming_filter_matches_only_tasks_for_my_own_unit()
    {
        var incoming = AssignedTaskRules.Incoming(ViewerOf(UserOf(Handler), AppPermissions.HandleUnitTasks)).Compile();
        var mine = UserTask(AssignedTaskStatus.Todo);
        var other = UserTask(AssignedTaskStatus.Todo);
        other.AssigneeUserId = Stranger;
        var myOffice = new AssignedTask { Title = "م", TargetType = AssignedTaskTargetType.Office, BranchId = Branch, DepartmentId = Department, OfficeId = Office };
        var otherOffice = new AssignedTask { Title = "م", TargetType = AssignedTaskTargetType.Office, BranchId = Branch, DepartmentId = Department, OfficeId = 101 };

        Assert.True(incoming(mine));
        Assert.False(incoming(other));
        Assert.True(incoming(myOffice));
        Assert.False(incoming(otherOffice));
    }
}
