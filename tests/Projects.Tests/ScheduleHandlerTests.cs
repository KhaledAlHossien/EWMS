using Application.Common;
using Application.DTOs.Request;
using Application.Features.Schedules;
using Application.Interfaces;
using Domain.Entities;
using NSubstitute;

namespace Projects.Tests;

/// <summary>معالج الجدول الزمني: حدّ الرؤية، فصل الإعداد عن الاعتماد، تثبيت الأصل، وأن المعتمد لا يُحرَّر كالمسودة</summary>
public class ScheduleHandlerTests
{
    private const int Me = 2, Other = 3, ScheduleId = 7;

    private sealed class Fixture
    {
        public IScheduleService Schedules = Substitute.For<IScheduleService>();
        public IProjectService Projects = Substitute.For<IProjectService>();
        public HandlerFacade Handler = null!;
    }

    private static User UserOf(int id) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1,
        Role = new Role { Id = 1, Name = "Emp" }, BranchId = 1, DepartmentId = 10
    };

    private static Fixture Create(int viewerId, params string[] permissions)
    {
        var users = Substitute.For<IUserService>();
        users.UserId.Returns(viewerId);
        users.GetByIdAsync(viewerId).Returns(UserOf(viewerId));
        var perms = Substitute.For<IUserPermissionService>();
        perms.GetAsync(viewerId).Returns((IReadOnlySet<string>)new HashSet<string>(permissions.Append(AppPermissions.ViewProjects)));

        var f = new Fixture();
        f.Handler = new HandlerFacade(new ScheduleContext(f.Schedules, f.Projects, users, perms));
        return f;
    }

    private static Project ProjectOf(ProjectStage stage = ProjectStage.Execution, params int[] memberIds) => new()
    {
        Id = 5, Name = "مشروع", BranchId = 1, Stage = stage, CreatedAt = new DateTime(2026, 10, 27),
        Members = memberIds.Select(i => new ProjectMember { ProjectId = 5, UserId = i, User = UserOf(i) }).ToList()
    };

    private static ScheduleTask TaskOf(int id, string name = "حفر", int start = 1, int days = 10, int progress = 0,
        ScheduleTaskStatus status = ScheduleTaskStatus.NotStarted, bool locked = false)
    {
        var from = new DateTime(2026, 11, 1).AddDays(start - 1);
        return new ScheduleTask
        {
            Id = id, SortOrder = id, Name = name, PlannedStart = from, PlannedEnd = from.AddDays(days - 1), Weight = 1,
            Progress = progress, Status = status, OriginalStart = locked ? from : null, OriginalEnd = locked ? from.AddDays(days - 1) : null
        };
    }

    private static ProjectSchedule ScheduleOf(Project project, ScheduleStatus status, int createdBy = Other, params ScheduleTask[] tasks)
    {
        var schedule = new ProjectSchedule
        {
            Id = ScheduleId, ProjectId = project.Id, Project = project, VersionNumber = 1, Status = status,
            CreatedByUserId = createdBy, CreatedByUser = UserOf(createdBy), CreatedAt = DateTime.UtcNow, Tasks = tasks.ToList()
        };
        foreach (var t in tasks) { t.ScheduleId = ScheduleId; t.Schedule = schedule; }
        return schedule;
    }

    private static ScheduleTaskRequestDto Dto(ScheduleTask t, Action<ScheduleTaskRequestDto>? tweak = null)
    {
        var dto = new ScheduleTaskRequestDto { Name = t.Name, PlannedStart = t.PlannedStart, PlannedEnd = t.PlannedEnd, Weight = t.Weight };
        tweak?.Invoke(dto);
        return dto;
    }

    // ───────── الحدّ ─────────

    [Fact]
    public async Task A_project_outside_my_scope_has_no_schedule_overview()
    {
        var f = Create(Me, AppPermissions.ViewProjectSchedule);
        f.Projects.GetByIdAsync(5).Returns(ProjectOf());   // لست مشاركاً

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Handler.Handle(new GetScheduleOverviewQuery(5), default));
        await f.Schedules.DidNotReceive().GetVersionsAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task A_schedule_of_a_project_outside_my_scope_is_not_readable_nor_editable()
    {
        var f = Create(Me, AppPermissions.ViewProjectSchedule, AppPermissions.EditProjectSchedule);
        var task = TaskOf(1);
        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(ProjectOf(), ScheduleStatus.Draft, Other, task));
        f.Schedules.GetTaskAsync(1).Returns(task);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Handler.Handle(new GetScheduleQuery(ScheduleId), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Handler.Handle(new UpdateScheduleTaskCommand(1, Dto(task)), default));
        await f.Schedules.DidNotReceive().UpdateTaskAsync(Arg.Any<ScheduleTask>(), Arg.Any<byte[]?>(), Arg.Any<AuditEntry>(), Arg.Any<int>());
    }

    // ───────── المسودة والاعتماد ─────────

    [Fact]
    public async Task A_revision_copies_the_approved_tasks_and_dependencies()
    {
        var f = Create(Me, AppPermissions.EditProjectSchedule);
        var project = ProjectOf(ProjectStage.Execution, Me);
        var a = TaskOf(1, locked: true); var b = TaskOf(2, start: 12, locked: true);
        var approved = ScheduleOf(project, ScheduleStatus.Approved, Other, a, b);
        f.Projects.GetByIdAsync(5).Returns(project);
        f.Schedules.GetVersionsAsync(5).Returns([new ScheduleVersionRow(approved, 2, 0)]);
        f.Schedules.GetApprovedAsync(5).Returns(approved);
        f.Schedules.GetDependenciesAsync(ScheduleId).Returns([new ScheduleDependency { Id = 1, ScheduleId = ScheduleId, PredecessorId = 1, SuccessorId = 2 }]);
        f.Schedules.CreateDraftAsync(default!, default!, default!, default!, default).ReturnsForAnyArgs(ci => { var d = ci.ArgAt<ProjectSchedule>(0); d.Id = 8; return d; });
        f.Schedules.GetByIdAsync(8).Returns(ScheduleOf(project, ScheduleStatus.Draft, Me));

        await f.Handler.Handle(new CreateScheduleDraftCommand(5, new ScheduleDraftRequestDto { Title = "تعديل" }), default);

        await f.Schedules.Received(1).CreateDraftAsync(
            Arg.Is<ProjectSchedule>(d => d.Status == ScheduleStatus.Draft && d.CreatedByUserId == Me && d.Title == "تعديل"),
            Arg.Is<IReadOnlyList<(int OldId, ScheduleTask Copy)>>(c => c.Count == 2 && c.All(x => x.Copy.OriginalStart != null)),   // الأصل يُنقل
            Arg.Is<IReadOnlyList<ScheduleRules.Edge>>(e => e.Count == 1 && e[0].PredecessorId == 1 && e[0].SuccessorId == 2),
            Arg.Any<AuditEntry>(), 1);
    }

    [Fact]
    public async Task Only_one_draft_exists_and_no_new_version_while_an_extension_is_pending()
    {
        var f = Create(Me, AppPermissions.EditProjectSchedule);
        var project = ProjectOf(ProjectStage.Execution, Me);
        f.Projects.GetByIdAsync(5).Returns(project);

        f.Schedules.GetVersionsAsync(5).Returns([new ScheduleVersionRow(ScheduleOf(project, ScheduleStatus.Draft), 0, 0)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleDraftCommand(5, new ScheduleDraftRequestDto()), default));

        f.Schedules.GetVersionsAsync(5).Returns([new ScheduleVersionRow(ScheduleOf(project, ScheduleStatus.Approved), 1, 0)]);
        f.Schedules.HasPendingExtensionAsync(5).Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleDraftCommand(5, new ScheduleDraftRequestDto()), default));

        await f.Schedules.DidNotReceiveWithAnyArgs().CreateDraftAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task The_creator_of_a_draft_never_approves_it()
    {
        var f = Create(Me, AppPermissions.ApproveProjectSchedule);
        var project = ProjectOf(ProjectStage.Execution, Me);
        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(project, ScheduleStatus.Draft, createdBy: Me, TaskOf(1)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new ApproveScheduleCommand(ScheduleId), default));
        await f.Schedules.DidNotReceiveWithAnyArgs().ApproveAsync(default!, default, default, default!, default);
    }

    [Fact]
    public async Task A_draft_without_tasks_or_with_a_cycle_is_not_approved()
    {
        var f = Create(Me, AppPermissions.ApproveProjectSchedule);
        var project = ProjectOf(ProjectStage.Execution, Me);

        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(project, ScheduleStatus.Draft, Other));
        f.Schedules.GetDependenciesAsync(ScheduleId).Returns([]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new ApproveScheduleCommand(ScheduleId), default));

        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(project, ScheduleStatus.Draft, Other, TaskOf(1), TaskOf(2, start: 12)));
        f.Schedules.GetDependenciesAsync(ScheduleId).Returns([
            new ScheduleDependency { Id = 1, PredecessorId = 1, SuccessorId = 2 }, new ScheduleDependency { Id = 2, PredecessorId = 2, SuccessorId = 1 }]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new ApproveScheduleCommand(ScheduleId), default));

        await f.Schedules.DidNotReceiveWithAnyArgs().ApproveAsync(default!, default, default, default!, default);
    }

    [Fact]
    public async Task Another_user_approves_a_valid_draft_and_replaces_the_previous_approved_one()
    {
        var f = Create(Me, AppPermissions.ApproveProjectSchedule);
        var project = ProjectOf(ProjectStage.Execution, Me);
        var draft = ScheduleOf(project, ScheduleStatus.Draft, Other, TaskOf(1), TaskOf(2, start: 12));
        var previous = new ProjectSchedule { Id = 3, ProjectId = 5, VersionNumber = 1, Status = ScheduleStatus.Approved };
        f.Schedules.GetByIdAsync(ScheduleId).Returns(draft);
        f.Schedules.GetApprovedAsync(5).Returns(previous);
        f.Schedules.GetDependenciesAsync(ScheduleId).Returns([new ScheduleDependency { Id = 1, PredecessorId = 1, SuccessorId = 2 }]);

        await f.Handler.Handle(new ApproveScheduleCommand(ScheduleId), default);

        await f.Schedules.Received(1).ApproveAsync(draft, previous, Me,
            Arg.Is<AuditEntry>(a => a.Action == AuditActions.Approved && a.UserId == Me), 1);
    }

    // ───────── البنود ─────────

    [Fact]
    public async Task Tasks_are_added_to_a_draft_only_and_not_to_an_approved_or_finished_project()
    {
        var f = Create(Me, AppPermissions.EditProjectSchedule);
        var open = ProjectOf(ProjectStage.Execution, Me);
        var dto = new ScheduleTaskRequestDto { Name = "بند", PlannedStart = new DateTime(2026, 11, 1), PlannedEnd = new DateTime(2026, 11, 5), Weight = 2 };

        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(open, ScheduleStatus.Approved, Other, TaskOf(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new AddScheduleTaskCommand(ScheduleId, dto), default));

        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(ProjectOf(ProjectStage.Closed, Me), ScheduleStatus.Draft));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new AddScheduleTaskCommand(ScheduleId, dto), default));

        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(open, ScheduleStatus.Draft, Other, TaskOf(1)));
        var added = await f.Handler.Handle(new AddScheduleTaskCommand(ScheduleId, dto), default);
        Assert.Equal(2, added.SortOrder);
        Assert.Equal(2m, added.Weight);
        Assert.False(added.DatesLocked);
        await f.Schedules.Received(1).AddTaskAsync(Arg.Any<ScheduleTask>(), Arg.Any<AuditEntry>(), 1);
    }

    [Fact]
    public async Task A_task_needs_a_positive_weight_valid_dates_and_a_project_member_as_assignee()
    {
        var f = Create(Me, AppPermissions.EditProjectSchedule);
        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(ProjectOf(ProjectStage.Execution, Me), ScheduleStatus.Draft));
        var good = new ScheduleTaskRequestDto { Name = "بند", PlannedStart = new DateTime(2026, 11, 1), PlannedEnd = new DateTime(2026, 11, 5), Weight = 1 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new AddScheduleTaskCommand(ScheduleId,
            new ScheduleTaskRequestDto { Name = "بند", PlannedStart = good.PlannedStart, PlannedEnd = good.PlannedEnd, Weight = 0 }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new AddScheduleTaskCommand(ScheduleId,
            new ScheduleTaskRequestDto { Name = "بند", PlannedStart = good.PlannedEnd, PlannedEnd = good.PlannedStart, Weight = 1 }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new AddScheduleTaskCommand(ScheduleId,
            new ScheduleTaskRequestDto { Name = "بند", PlannedStart = good.PlannedStart, PlannedEnd = good.PlannedEnd, Weight = 1, AssigneeUserId = 99 }), default));

        await f.Schedules.DidNotReceiveWithAnyArgs().AddTaskAsync(default!, default!, default);
    }

    [Fact]
    public async Task The_dates_of_a_carried_over_task_are_fixed_but_its_weight_is_editable()
    {
        var f = Create(Me, AppPermissions.EditProjectSchedule);
        var project = ProjectOf(ProjectStage.Execution, Me);
        var carried = TaskOf(1, locked: true);
        var schedule = ScheduleOf(project, ScheduleStatus.Draft, Other, carried);
        f.Schedules.GetTaskAsync(1).Returns(carried);
        f.Schedules.GetByIdAsync(ScheduleId).Returns(schedule);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new UpdateScheduleTaskCommand(1,
            Dto(carried, d => d.PlannedEnd = carried.PlannedEnd.AddDays(3))), default));

        var dto = await f.Handler.Handle(new UpdateScheduleTaskCommand(1, Dto(carried, d => d.Weight = 5)), default);
        Assert.Equal(5m, dto.Weight);
        Assert.True(dto.DatesLocked);
        await f.Schedules.Received(1).UpdateTaskAsync(carried, Arg.Any<byte[]?>(), Arg.Is<AuditEntry>(a => a.Action == AuditActions.Updated), 1);
    }

    [Fact]
    public async Task A_task_without_changes_is_not_saved_again()
    {
        var f = Create(Me, AppPermissions.EditProjectSchedule);
        var task = TaskOf(1);
        ScheduleOf(ProjectOf(ProjectStage.Execution, Me), ScheduleStatus.Draft, Other, task);
        f.Schedules.GetTaskAsync(1).Returns(task);

        await f.Handler.Handle(new UpdateScheduleTaskCommand(1, Dto(task)), default);

        await f.Schedules.DidNotReceiveWithAnyArgs().UpdateTaskAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task A_dependency_must_stay_inside_the_draft_without_cycles_or_repeats()
    {
        var f = Create(Me, AppPermissions.EditProjectSchedule);
        var project = ProjectOf(ProjectStage.Execution, Me);
        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(project, ScheduleStatus.Draft, Other, TaskOf(1), TaskOf(2, start: 12), TaskOf(3, start: 25)));
        f.Schedules.GetDependenciesAsync(ScheduleId).Returns([
            new ScheduleDependency { Id = 1, PredecessorId = 1, SuccessorId = 2 }, new ScheduleDependency { Id = 2, PredecessorId = 2, SuccessorId = 3 }]);

        Task<Application.DTOs.Response.ScheduleDependencyResponseDto> Add(int p, int s) => f.Handler.Handle(new AddScheduleDependencyCommand(ScheduleId, new ScheduleDependencyRequestDto { PredecessorId = p, SuccessorId = s }), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Add(1, 1));    // على نفسه
        await Assert.ThrowsAsync<InvalidOperationException>(() => Add(1, 99));   // بند من خارج المسودة
        await Assert.ThrowsAsync<InvalidOperationException>(() => Add(1, 2));    // مكررة
        await Assert.ThrowsAsync<InvalidOperationException>(() => Add(3, 1));    // حلقة غير مباشرة
        await f.Schedules.DidNotReceiveWithAnyArgs().AddDependencyAsync(default!, default!, default);

        var added = await Add(1, 3);
        Assert.False(added.HasConflict);
        await f.Schedules.Received(1).AddDependencyAsync(Arg.Any<ScheduleDependency>(), Arg.Any<AuditEntry>(), 1);
    }

    [Fact]
    public async Task Reordering_needs_exactly_the_tasks_of_the_draft()
    {
        var f = Create(Me, AppPermissions.EditProjectSchedule);
        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(ProjectOf(ProjectStage.Execution, Me), ScheduleStatus.Draft, Other, TaskOf(1), TaskOf(2, start: 12)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new ReorderScheduleTasksCommand(ScheduleId, new ReorderScheduleTasksRequestDto { TaskIds = [1] }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new ReorderScheduleTasksCommand(ScheduleId, new ReorderScheduleTasksRequestDto { TaskIds = [1, 1] }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new ReorderScheduleTasksCommand(ScheduleId, new ReorderScheduleTasksRequestDto { TaskIds = [1, 9] }), default));

        await f.Handler.Handle(new ReorderScheduleTasksCommand(ScheduleId, new ReorderScheduleTasksRequestDto { TaskIds = [2, 1] }), default);
        await f.Schedules.Received(1).ReorderAsync(ScheduleId, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 2, 1 })), Arg.Any<AuditEntry>(), 1);
    }

    // ───────── الإنجاز والتصنيف ─────────

    private static UpdateScheduleProgressCommand Progress(int taskId, ScheduleTaskStatus status, int progress, string? note = null) =>
        new(taskId, new UpdateScheduleProgressRequestDto { Status = (int)status, Progress = progress, Note = note });

    [Fact]
    public async Task Progress_is_updated_on_the_approved_schedule_only()
    {
        var f = Create(Me, AppPermissions.UpdateScheduleProgress);
        var project = ProjectOf(ProjectStage.Execution, Me);
        var task = TaskOf(1, locked: true);
        var draftTask = TaskOf(2);
        ScheduleOf(project, ScheduleStatus.Approved, Other, task);
        ScheduleOf(project, ScheduleStatus.Draft, Other, draftTask);
        f.Schedules.GetTaskAsync(1).Returns(task);
        f.Schedules.GetTaskAsync(2).Returns(draftTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(Progress(2, ScheduleTaskStatus.InProgress, 20), default));

        var dto = await f.Handler.Handle(Progress(1, ScheduleTaskStatus.InProgress, 40), default);
        Assert.Equal(40, dto.Progress);
        Assert.Equal(DateTime.Today, task.ActualStart);   // البداية الفعلية تُسجَّل تلقائياً
        await f.Schedules.Received(1).SaveProgressAsync(task,
            Arg.Is<ScheduleProgressEntry>(e => e.Progress == 40 && e.UserId == Me && e.Status == ScheduleTaskStatus.InProgress),
            Arg.Any<byte[]?>(), Arg.Is<AuditEntry>(a => a.Action == AuditActions.Progress), 1);
    }

    [Fact]
    public async Task Progress_values_must_be_consistent_and_lowering_needs_a_note()
    {
        var f = Create(Me, AppPermissions.UpdateScheduleProgress);
        var task = TaskOf(1, locked: true, progress: 60, status: ScheduleTaskStatus.InProgress);
        ScheduleOf(ProjectOf(ProjectStage.Execution, Me), ScheduleStatus.Approved, Other, task);
        f.Schedules.GetTaskAsync(1).Returns(task);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(Progress(1, ScheduleTaskStatus.InProgress, 100), default));   // 100% = مكتمل
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(Progress(1, ScheduleTaskStatus.Completed, 80), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(Progress(1, ScheduleTaskStatus.InProgress, 30), default));    // خفض بلا ملاحظة
        Assert.Equal(60, task.Progress);   // لم يتغير شيء
        await f.Schedules.DidNotReceiveWithAnyArgs().SaveProgressAsync(default!, default!, default, default!, default);

        await f.Handler.Handle(Progress(1, ScheduleTaskStatus.InProgress, 30, "إعادة قياس الكميات"), default);
        Assert.Equal(30, task.Progress);
    }

    [Fact]
    public async Task Completing_a_task_records_its_actual_end_and_a_repeat_changes_nothing()
    {
        var f = Create(Me, AppPermissions.UpdateScheduleProgress);
        var task = TaskOf(1, locked: true, progress: 50, status: ScheduleTaskStatus.InProgress);
        ScheduleOf(ProjectOf(ProjectStage.Execution, Me), ScheduleStatus.Approved, Other, task);
        f.Schedules.GetTaskAsync(1).Returns(task);

        await f.Handler.Handle(Progress(1, ScheduleTaskStatus.Completed, 100), default);
        Assert.Equal(DateTime.Today, task.ActualEnd);

        f.Schedules.ClearReceivedCalls();
        await f.Handler.Handle(Progress(1, ScheduleTaskStatus.Completed, 100), default);   // لا جديد: لا سطر في السجل
        await f.Schedules.DidNotReceiveWithAnyArgs().SaveProgressAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Classification_is_manual_audited_and_closed_for_old_versions()
    {
        var f = Create(Me, AppPermissions.ClassifyScheduleTask);
        var project = ProjectOf(ProjectStage.Execution, Me);
        var task = TaskOf(1, locked: true);
        var old = TaskOf(2);
        ScheduleOf(project, ScheduleStatus.Approved, Other, task);
        ScheduleOf(project, ScheduleStatus.Superseded, Other, old);
        f.Schedules.GetTaskAsync(1).Returns(task);
        f.Schedules.GetTaskAsync(2).Returns(old);

        var dto = await f.Handler.Handle(new ClassifyScheduleTaskCommand(1, new ClassifyScheduleTaskRequestDto { IsCritical = true }), default);
        Assert.True(dto.IsCritical);
        await f.Schedules.Received(1).UpdateTaskAsync(task, Arg.Any<byte[]?>(), Arg.Is<AuditEntry>(a => a.Action == AuditActions.Classified), 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Handler.Handle(new ClassifyScheduleTaskCommand(2, new ClassifyScheduleTaskRequestDto { IsCritical = true }), default));
    }

    // ───────── أمر المباشرة ─────────

    private static StartOrderRequestDto Order(Action<StartOrderRequestDto>? tweak = null)
    {
        var dto = new StartOrderRequestDto
        {
            OrderNumber = " م/15 ", OrderDate = new DateTime(2026, 10, 1), StartDate = new DateTime(2026, 10, 10), CompletionDays = 180, ExecutionDays = 150
        };
        tweak?.Invoke(dto);
        return dto;
    }

    [Fact]
    public async Task A_start_order_is_created_with_its_audit_and_engineers_must_be_members()
    {
        var f = Create(Me, AppPermissions.EditProjectStartOrder);
        f.Projects.GetByIdAsync(5).Returns(ProjectOf(ProjectStage.Execution, Me));
        f.Schedules.GetStartOrderAsync(5).Returns((ProjectStartOrder?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new SaveStartOrderCommand(5, Order(d => d.SiteEngineerUserId = Other)), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new SaveStartOrderCommand(5, Order(d => d.StartDate = new DateTime(2026, 9, 1))), default));
        await f.Schedules.DidNotReceiveWithAnyArgs().SaveStartOrderAsync(default!, default, default, default!, default);

        await f.Handler.Handle(new SaveStartOrderCommand(5, Order(d => d.SiteEngineerUserId = Me)), default);
        await f.Schedules.Received(1).SaveStartOrderAsync(
            Arg.Is<ProjectStartOrder>(o => o.ProjectId == 5 && o.OrderNumber == "م/15" && o.SiteEngineerUserId == Me && o.CreatedByUserId == Me),
            true, Arg.Any<byte[]?>(), Arg.Is<AuditEntry>(a => a.Action == AuditActions.Created && a.EntityType == AuditEntities.StartOrder), 1);
    }

    [Fact]
    public async Task Saving_an_unchanged_start_order_writes_nothing()
    {
        var f = Create(Me, AppPermissions.EditProjectStartOrder);
        f.Projects.GetByIdAsync(5).Returns(ProjectOf(ProjectStage.Execution, Me));
        f.Schedules.GetStartOrderAsync(5).Returns(new ProjectStartOrder
        {
            Id = 1, ProjectId = 5, OrderNumber = "م/15", OrderDate = new DateTime(2026, 10, 1), StartDate = new DateTime(2026, 10, 10),
            CompletionDays = 180, ExecutionDays = 150
        });

        await f.Handler.Handle(new SaveStartOrderCommand(5, Order()), default);

        await f.Schedules.DidNotReceiveWithAnyArgs().SaveStartOrderAsync(default!, default, default, default!, default);
    }

    // ───────── التفصيل والتنبيهات ─────────

    [Fact]
    public async Task The_detail_reports_progress_flags_and_warnings_without_blocking_anything()
    {
        var f = Create(Me, AppPermissions.ViewProjectSchedule, AppPermissions.ApproveProjectSchedule, AppPermissions.EditProjectSchedule);
        var project = ProjectOf(ProjectStage.Execution, Me);
        var done = TaskOf(1, progress: 100, status: ScheduleTaskStatus.Completed);
        var half = TaskOf(2, start: 12, progress: 0);
        half.Weight = 3;
        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(project, ScheduleStatus.Draft, Other, done, half));
        f.Schedules.GetDependenciesAsync(ScheduleId).Returns([new ScheduleDependency { Id = 1, PredecessorId = 1, SuccessorId = 2 }]);
        f.Schedules.GetStartOrderAsync(5).Returns((ProjectStartOrder?)null);

        var dto = await f.Handler.Handle(new GetScheduleQuery(ScheduleId), default);

        Assert.Equal(25m, dto.ActualProgress);   // (100×1 + 0×3) / 4
        Assert.True(dto.CanEdit);
        Assert.True(dto.CanApprove);             // أنشأه غيري
        Assert.False(dto.CanUpdateProgress);     // مسودة
        Assert.Contains(dto.Warnings, w => w.Contains("أمر مباشرة"));
        Assert.Equal(2, dto.Tasks.Count);
    }

    [Fact]
    public async Task I_cannot_approve_what_I_created_and_a_finished_project_is_read_only()
    {
        var f = Create(Me, AppPermissions.ViewProjectSchedule, AppPermissions.ApproveProjectSchedule, AppPermissions.EditProjectSchedule);
        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(ProjectOf(ProjectStage.Execution, Me), ScheduleStatus.Draft, createdBy: Me, TaskOf(1)));
        f.Schedules.GetDependenciesAsync(ScheduleId).Returns([]);
        var mine = await f.Handler.Handle(new GetScheduleQuery(ScheduleId), default);
        Assert.True(mine.CanEdit);
        Assert.False(mine.CanApprove);

        f.Schedules.GetByIdAsync(ScheduleId).Returns(ScheduleOf(ProjectOf(ProjectStage.Closed, Me), ScheduleStatus.Draft, Other, TaskOf(1)));
        var closed = await f.Handler.Handle(new GetScheduleQuery(ScheduleId), default);
        Assert.False(closed.CanEdit);
        Assert.False(closed.CanApprove);
    }
}
