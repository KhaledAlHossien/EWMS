using Application.Common;
using Application.DTOs.Request;
using Application.Features.Schedules;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using NSubstitute;

namespace Projects.Tests;

/// <summary>التأخير والتمديد: النظام يسجّل ولا يقرّر، ومن سجّل أو طلب لا يعتمد، والأصل ثابت والحالي لا يتغير إلا باعتماد</summary>
public class ScheduleChangeHandlerTests
{
    private const int Me = 2, Other = 3, ScheduleId = 7, TaskId = 1;

    private sealed class Fixture
    {
        public IScheduleChangeService Changes = Substitute.For<IScheduleChangeService>();
        public IScheduleService Schedules = Substitute.For<IScheduleService>();
        public IProjectService Projects = Substitute.For<IProjectService>();
        public IProjectDocumentService Documents = Substitute.For<IProjectDocumentService>();
        public IPublicHolidayService Holidays = Substitute.For<IPublicHolidayService>();
        public INotificationService Notifications = Substitute.For<INotificationService>();
        public IUserPermissionService Permissions = Substitute.For<IUserPermissionService>();
        public ScheduleChangeHandler Handler = null!;
    }

    private static User UserOf(int id) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1,
        Role = new Role { Id = 1, Name = "Emp" }, BranchId = 1, DepartmentId = 10
    };

    private static Fixture Create(int viewerId, params string[] permissions)
    {
        var f = new Fixture();
        var users = Substitute.For<IUserService>();
        users.UserId.Returns(viewerId);
        users.GetByIdAsync(viewerId).Returns(UserOf(viewerId));
        f.Permissions.GetAsync(viewerId).Returns((IReadOnlySet<string>)new HashSet<string>(permissions.Append(AppPermissions.ViewProjects)));
        f.Permissions.GetAsync(Other).Returns((IReadOnlySet<string>)new HashSet<string> { AppPermissions.ViewProjects });
        f.Handler = new ScheduleChangeHandler(f.Changes, f.Schedules, f.Projects, f.Documents, f.Holidays, f.Notifications, users, f.Permissions);
        return f;
    }

    private static Project ProjectOf(ProjectStage stage = ProjectStage.Execution, params int[] memberIds) => new()
    {
        Id = 5, Name = "مشروع", BranchId = 1, Stage = stage, CreatedAt = new DateTime(2026, 10, 27),
        Members = memberIds.Select(i => new ProjectMember { ProjectId = 5, UserId = i, User = UserOf(i) }).ToList()
    };

    private static ScheduleTask TaskOf(int id, string name, DateTime start, DateTime end, ScheduleTaskStatus status = ScheduleTaskStatus.InProgress) => new()
    {
        Id = id, SortOrder = id, Name = name, PlannedStart = start, PlannedEnd = end, OriginalStart = start, OriginalEnd = end, Weight = 1, Status = status
    };

    private static ProjectSchedule ApprovedOf(Project project, params ScheduleTask[] tasks)
    {
        var schedule = new ProjectSchedule
        {
            Id = ScheduleId, ProjectId = project.Id, Project = project, VersionNumber = 1, Status = ScheduleStatus.Approved,
            CreatedByUserId = Other, CreatedByUser = UserOf(Other), Tasks = tasks.ToList()
        };
        foreach (var t in tasks) { t.ScheduleId = ScheduleId; t.Schedule = schedule; }
        return schedule;
    }

    private static readonly DateTime Past = DateTime.Today.AddDays(-20);

    // مشروع أنا وغيري مشاركان فيه، وجدول معتمد فيه بند متأخر وبند لاحق
    private static (Fixture F, Project Project, ProjectSchedule Schedule, ScheduleTask Late, ScheduleTask Next) Setup(params string[] permissions)
    {
        var f = Create(Me, permissions);
        var project = ProjectOf(ProjectStage.Execution, Me, Other);
        var late = TaskOf(TaskId, "حفر", Past.AddDays(-10), Past);
        var next = TaskOf(2, "صب", Past.AddDays(1), Past.AddDays(10), ScheduleTaskStatus.NotStarted);
        var schedule = ApprovedOf(project, late, next);
        f.Projects.GetByIdAsync(5).Returns(project);
        f.Schedules.GetTaskAsync(TaskId).Returns(late);
        f.Schedules.GetByIdAsync(ScheduleId).Returns(schedule);
        f.Schedules.GetApprovedAsync(5).Returns(schedule);
        f.Schedules.GetDependenciesAsync(ScheduleId).Returns([new ScheduleDependency { Id = 1, ScheduleId = ScheduleId, PredecessorId = TaskId, SuccessorId = 2 }]);
        return (f, project, schedule, late, next);
    }

    private static ScheduleDelay DelayOf(Project project, ScheduleTask task, DelayKind kind, DelayStatus status, int recordedBy) => new()
    {
        Id = 20, ProjectId = project.Id, Project = project, TaskId = task.Id, Task = task, Kind = kind, Status = status,
        DiscoveredAt = DateTime.Today, PlannedEndAtDiscovery = task.PlannedEnd, RecordedByUserId = recordedBy, RecordedByUser = UserOf(recordedBy),
        RecordedAt = DateTime.UtcNow
    };

    // ════════════════════ التأخير ════════════════════

    [Fact]
    public async Task A_delay_is_recorded_only_by_a_person_on_an_approved_schedule_and_never_automatically()
    {
        var (f, project, schedule, late, _) = Setup(AppPermissions.RecordScheduleDelay);
        f.Changes.GetDelayAsync(Arg.Any<int>()).Returns(ci => DelayOf(project, late, DelayKind.Unjustified, DelayStatus.Recorded, Me));
        var dto = new ScheduleDelayRequestDto { Kind = 1, DiscoveredAt = DateTime.Today, Reason = "تأخر وصول المواد" };

        var created = await f.Handler.Handle(new RecordScheduleDelayCommand(TaskId, dto), default);

        await f.Changes.Received(1).AddDelayAsync(
            Arg.Is<ScheduleDelay>(d => d.Status == DelayStatus.Recorded && d.RecordedByUserId == Me && d.PlannedEndAtDiscovery == late.PlannedEnd.Date && d.TaskId == TaskId),
            Arg.Is<AuditEntry>(a => a.Action == AuditActions.Created && a.EntityType == AuditEntities.Delay), 1);
        Assert.Equal("مسجَّل", created.StatusName);

        schedule.Status = ScheduleStatus.Superseded;     // إصدار سابق
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new RecordScheduleDelayCommand(TaskId, dto), default));
    }

    [Fact]
    public async Task An_unjustified_delay_needs_a_note_a_completed_task_takes_no_delay_and_only_one_live_delay_exists()
    {
        var (f, _, _, late, _) = Setup(AppPermissions.RecordScheduleDelay);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new RecordScheduleDelayCommand(TaskId, new ScheduleDelayRequestDto { Kind = 1, DiscoveredAt = DateTime.Today }), default));      // بلا سبب ولا إجراء
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new RecordScheduleDelayCommand(TaskId, new ScheduleDelayRequestDto { Kind = 1, DiscoveredAt = DateTime.Today.AddDays(1), Reason = "x" }), default));   // اكتشاف في المستقبل

        f.Changes.HasLiveDelayAsync(TaskId).Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new RecordScheduleDelayCommand(TaskId, new ScheduleDelayRequestDto { Kind = 1, DiscoveredAt = DateTime.Today, Reason = "x" }), default));

        f.Changes.HasLiveDelayAsync(TaskId).Returns(false);
        late.Status = ScheduleTaskStatus.Completed;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new RecordScheduleDelayCommand(TaskId, new ScheduleDelayRequestDto { Kind = 1, DiscoveredAt = DateTime.Today, Reason = "x" }), default));

        await f.Changes.DidNotReceiveWithAnyArgs().AddDelayAsync(default!, default!, default);
    }

    [Fact]
    public async Task Affected_tasks_must_belong_to_the_same_schedule_and_not_be_the_delayed_task()
    {
        var (f, _, _, _, _) = Setup(AppPermissions.RecordScheduleDelay);
        ScheduleDelayRequestDto Dto(params int[] ids) => new() { Kind = 1, DiscoveredAt = DateTime.Today, Reason = "x", AffectedTaskIds = ids.ToList() };

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new RecordScheduleDelayCommand(TaskId, Dto(99)), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new RecordScheduleDelayCommand(TaskId, Dto(TaskId)), default));
        await f.Changes.DidNotReceiveWithAnyArgs().AddDelayAsync(default!, default!, default);
    }

    [Fact]
    public async Task A_delay_of_a_project_outside_my_scope_is_neither_read_nor_recorded()
    {
        var f = Create(Me, AppPermissions.RecordScheduleDelay, AppPermissions.ViewProjectSchedule);
        var project = ProjectOf();     // لست مشاركاً
        var task = TaskOf(TaskId, "حفر", Past.AddDays(-5), Past);
        ApprovedOf(project, task);
        f.Schedules.GetTaskAsync(TaskId).Returns(task);
        f.Changes.GetDelayAsync(20).Returns(DelayOf(project, task, DelayKind.Unjustified, DelayStatus.Recorded, Other));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Handler.Handle(new GetScheduleDelayQuery(20), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Handler.Handle(
            new RecordScheduleDelayCommand(TaskId, new ScheduleDelayRequestDto { DiscoveredAt = DateTime.Today, Reason = "x" }), default));
    }

    private static JustifyScheduleDelayRequestDto Justification(int? doc = 40) => new()
    {
        Reason = "منع وصول بسبب إغلاق الطريق", ApprovalAuthority = "إدارة المشروع", ApprovalDate = DateTime.Today.AddDays(-1), ApprovalDocumentVersionId = doc
    };

    private static ProjectDocumentVersion VersionOf(int projectId) => new()
    {
        Id = 40, VersionNumber = 1, FileName = "approval.pdf", Document = new ProjectDocument { Id = 9, ProjectId = projectId, Title = "موافقة" }
    };

    [Fact]
    public async Task Only_the_recorder_justifies_with_a_document_from_the_same_project_and_the_approvers_are_told()
    {
        var (f, project, _, late, _) = Setup(AppPermissions.RecordScheduleDelay, AppPermissions.ApproveDelayJustification);
        var mine = DelayOf(project, late, DelayKind.Unjustified, DelayStatus.Recorded, Me);
        f.Changes.GetDelayAsync(20).Returns(mine);
        f.Permissions.GetUsersWithPermissionAsync(AppPermissions.ApproveDelayJustification, Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>())
            .Returns([UserOf(Other)]);

        f.Documents.GetVersionAsync(40).Returns(VersionOf(projectId: 99));        // مستند من مشروع آخر
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new JustifyScheduleDelayCommand(20, Justification()), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new JustifyScheduleDelayCommand(20, Justification(doc: null)), default));   // بلا مستند

        f.Documents.GetVersionAsync(40).Returns(VersionOf(project.Id));
        var dto = await f.Handler.Handle(new JustifyScheduleDelayCommand(20, Justification()), default);

        Assert.Equal(DelayKind.Justified, mine.Kind);
        Assert.Equal(DelayStatus.Submitted, mine.Status);
        Assert.Equal((int)DelayStatus.Submitted, dto.Status);
        await f.Changes.Received(1).SaveDelayAsync(mine, Arg.Any<byte[]?>(), Arg.Is<AuditEntry>(a => a.Action == AuditActions.Justified), 1);
        await f.Notifications.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Notification>>(n =>
            n.Count() == 1 && n.First().UserId == Other && n.First().Type == NotificationType.ScheduleJustificationSubmitted));

        // غير المسجِّل لا يرفع تبرير تأخير غيره
        f.Changes.GetDelayAsync(21).Returns(DelayOf(project, late, DelayKind.Unjustified, DelayStatus.Recorded, Other));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new JustifyScheduleDelayCommand(21, Justification()), default));
    }

    [Fact]
    public async Task The_recorder_never_decides_his_own_justification_and_a_rejection_needs_a_reason()
    {
        var (f, project, _, late, _) = Setup(AppPermissions.ApproveDelayJustification);
        var submittedByMe = DelayOf(project, late, DelayKind.Justified, DelayStatus.Submitted, Me);
        var submittedByOther = DelayOf(project, late, DelayKind.Justified, DelayStatus.Submitted, Other);
        f.Changes.GetDelayAsync(20).Returns(submittedByMe);
        f.Changes.GetDelayAsync(21).Returns(submittedByOther);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new DecideScheduleDelayCommand(20, new ScheduleDecisionRequestDto { Approve = true }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new DecideScheduleDelayCommand(21, new ScheduleDecisionRequestDto { Approve = false }), default));       // رفض بلا سبب
        await f.Changes.DidNotReceiveWithAnyArgs().SaveDelayAsync(default!, default, default!, default);

        await f.Handler.Handle(new DecideScheduleDelayCommand(21, new ScheduleDecisionRequestDto { Approve = true, Note = "موافق" }), default);
        Assert.Equal(DelayStatus.Approved, submittedByOther.Status);
        Assert.Equal(Me, submittedByOther.DecidedByUserId);
        await f.Notifications.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Notification>>(n =>
            n.Count() == 1 && n.First().UserId == Other && n.First().Type == NotificationType.ScheduleJustificationDecided));
    }

    [Fact]
    public async Task A_live_delay_suggests_successors_for_review_but_selects_none_automatically()
    {
        var (f, project, _, late, next) = Setup(AppPermissions.ViewProjectSchedule);
        var delay = DelayOf(project, late, DelayKind.Unjustified, DelayStatus.Recorded, Me);
        f.Changes.GetDelayAsync(20).Returns(delay);

        var dto = await f.Handler.Handle(new GetScheduleDelayQuery(20), default);

        Assert.Empty(dto.AffectedTasks);
        Assert.Equal(next.Id, Assert.Single(dto.SuggestedTasks!).TaskId);
    }

    // ════════════════════ التمديد ════════════════════

    private static ScheduleExtensionRequestDto Extension(int days = 5, params int[] taskIds) => new()
    {
        Mode = 1, DayCount = 1, Reason = "ظروف الطقس",
        Lines = (taskIds.Length == 0 ? [TaskId] : taskIds).Select(id => new ExtensionLineRequestDto { TaskId = id, Days = days }).ToList()
    };

    [Fact]
    public async Task A_preview_changes_nothing_and_computes_new_ends_with_conflicts_and_suggestions()
    {
        var (f, _, _, late, next) = Setup(AppPermissions.RequestScheduleExtension);
        var end = late.PlannedEnd;

        var preview = await f.Handler.Handle(new PreviewScheduleExtensionCommand(5, Extension(5)), default);

        Assert.Equal(end.AddDays(5), Assert.Single(preview.Items).NewEnd);
        Assert.Equal(end, late.PlannedEnd);                             // لم يتغير شيء
        Assert.Equal(next.Id, Assert.Single(preview.SuggestedTasks).TaskId);   // لاحق مقترح للمراجعة فقط
        Assert.Single(preview.Conflicts);                                // اللاحق يبدأ قبل النهاية الجديدة للسابق
        await f.Changes.DidNotReceiveWithAnyArgs().AddExtensionAsync(default!, default!, default);
    }

    [Fact]
    public async Task Working_day_extensions_skip_fridays_and_the_public_holidays()
    {
        var (f, _, _, late, _) = Setup(AppPermissions.RequestScheduleExtension);
        late.PlannedEnd = new DateTime(2026, 11, 5);                    // خميس
        late.PlannedStart = new DateTime(2026, 11, 1);
        late.OriginalStart = late.PlannedStart; late.OriginalEnd = late.PlannedEnd;
        f.Holidays.GetDatesAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns((IReadOnlySet<DateTime>)new HashSet<DateTime> { new DateTime(2026, 11, 7) });
        var dto = Extension(2);
        dto.DayCount = 2;

        var preview = await f.Handler.Handle(new PreviewScheduleExtensionCommand(5, dto), default);

        // الجمعة 6 والسبت 7 (عطلة) لا يُحسبان: السبت المقبل أول يوم عمل بعد 5 ثم الأحد
        Assert.Equal(new DateTime(2026, 11, 9), Assert.Single(preview.Items).NewEnd);
    }

    [Fact]
    public async Task An_extension_needs_an_approved_schedule_valid_lines_and_a_single_pending_request()
    {
        var (f, _, _, _, _) = Setup(AppPermissions.RequestScheduleExtension);

        var mixed = Extension(5, TaskId, 2);
        mixed.Lines[1].Days = 7;                                         // جماعي بأيام مختلفة
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleExtensionCommand(5, mixed), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleExtensionCommand(5, Extension(0)), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleExtensionCommand(5, Extension(366)), default));

        f.Schedules.HasPendingExtensionAsync(5).Returns(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleExtensionCommand(5, Extension()), default));

        f.Schedules.HasPendingExtensionAsync(5).Returns(false);
        f.Schedules.GetApprovedAsync(5).Returns((ProjectSchedule?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleExtensionCommand(5, Extension()), default));

        await f.Changes.DidNotReceiveWithAnyArgs().AddExtensionAsync(default!, default!, default);
    }

    [Fact]
    public async Task A_linked_delay_must_be_justified_approved_and_on_the_current_schedule()
    {
        var (f, project, _, late, _) = Setup(AppPermissions.RequestScheduleExtension);
        var dto = Extension();
        dto.DelayId = 20;

        f.Changes.GetDelayAsync(20).Returns(DelayOf(project, late, DelayKind.Justified, DelayStatus.Submitted, Other));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleExtensionCommand(5, dto), default));
        await f.Changes.DidNotReceiveWithAnyArgs().AddExtensionAsync(default!, default!, default);
    }

    [Fact]
    public async Task A_draft_extension_is_saved_with_old_and_new_dates_and_stays_a_draft()
    {
        var (f, project, schedule, late, _) = Setup(AppPermissions.RequestScheduleExtension);
        var captured = (ScheduleExtension?)null;
        f.Changes.AddExtensionAsync(default!, default!, default).ReturnsForAnyArgs(ci => { captured = ci.ArgAt<ScheduleExtension>(0); captured.Id = 30; return Task.CompletedTask; });
        f.Changes.GetExtensionAsync(30).Returns(ci => { captured!.Project = project; captured.Schedule = schedule; foreach (var i in captured.Items) i.Task = late; return captured; });

        var dto = await f.Handler.Handle(new CreateScheduleExtensionCommand(5, Extension(5)), default);

        Assert.Equal(ExtensionStatus.Draft, captured!.Status);
        Assert.Equal(Me, captured.RequestedByUserId);
        var item = Assert.Single(captured.Items);
        Assert.Equal(late.PlannedEnd.Date, item.OldEnd);
        Assert.Equal(late.PlannedEnd.Date.AddDays(5), item.NewEnd);
        Assert.True(dto.CanEdit);
        Assert.False(dto.CanDecide);
        Assert.Equal(late.PlannedEnd.Date, late.PlannedEnd);                // لا تغيير على البند
    }

    private static ScheduleExtension ExtensionOf(Project project, ProjectSchedule schedule, ScheduleTask task, int days, ExtensionStatus status, int requestedBy)
    {
        var ext = new ScheduleExtension
        {
            Id = 30, ProjectId = project.Id, Project = project, ScheduleId = schedule.Id, Schedule = schedule, Mode = ExtensionMode.Group,
            DayCount = ExtensionDayCount.Calendar, Status = status, Reason = "طقس", RequestedByUserId = requestedBy, RequestedByUser = UserOf(requestedBy),
            RequestedAt = DateTime.UtcNow
        };
        ext.Items.Add(new ScheduleExtensionItem
        {
            TaskId = task.Id, Task = task, Days = days, OldStart = task.PlannedStart, OldEnd = task.PlannedEnd,
            NewStart = task.PlannedStart, NewEnd = task.PlannedEnd.AddDays(days)
        });
        return ext;
    }

    [Fact]
    public async Task Only_the_requester_edits_submits_or_deletes_his_draft()
    {
        var (f, project, schedule, late, _) = Setup(AppPermissions.RequestScheduleExtension);
        f.Changes.GetExtensionAsync(30).Returns(ExtensionOf(project, schedule, late, 5, ExtensionStatus.Draft, Other));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new SubmitScheduleExtensionCommand(30), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new DeleteScheduleExtensionCommand(30), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new UpdateScheduleExtensionCommand(30, Extension()), default));

        f.Changes.GetExtensionAsync(30).Returns(ExtensionOf(project, schedule, late, 5, ExtensionStatus.Submitted, Me));   // لم تعد مسودة
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new DeleteScheduleExtensionCommand(30), default));
        await f.Changes.DidNotReceiveWithAnyArgs().DeleteExtensionAsync(default!, default!, default);
    }

    [Fact]
    public async Task Submitting_notifies_the_approvers_and_a_request_on_changed_dates_is_not_submitted()
    {
        var (f, project, schedule, late, _) = Setup(AppPermissions.RequestScheduleExtension);
        var mine = ExtensionOf(project, schedule, late, 5, ExtensionStatus.Draft, Me);
        f.Changes.GetExtensionAsync(30).Returns(mine);
        f.Permissions.GetUsersWithPermissionAsync(AppPermissions.ApproveScheduleExtension, Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>())
            .Returns([UserOf(Other)]);

        await f.Handler.Handle(new SubmitScheduleExtensionCommand(30), default);

        Assert.Equal(ExtensionStatus.Submitted, mine.Status);
        await f.Changes.Received(1).SaveExtensionAsync(mine, Arg.Any<byte[]?>(), Arg.Is<AuditEntry>(a => a.Action == AuditActions.Submitted), 1);
        await f.Notifications.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Notification>>(n =>
            n.Single().UserId == Other && n.Single().Type == NotificationType.ScheduleExtensionSubmitted));

        var stale = ExtensionOf(project, schedule, late, 5, ExtensionStatus.Draft, Me);
        stale.Items.First().OldEnd = late.PlannedEnd.AddDays(-3);        // تاريخ البند تغيّر بعد الإعداد
        f.Changes.GetExtensionAsync(30).Returns(stale);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new SubmitScheduleExtensionCommand(30), default));
        Assert.Equal(ExtensionStatus.Draft, stale.Status);
    }

    [Fact]
    public async Task The_requester_never_decides_and_a_rejection_needs_a_reason()
    {
        var (f, project, schedule, late, _) = Setup(AppPermissions.ApproveScheduleExtension);
        f.Changes.GetExtensionAsync(30).Returns(ExtensionOf(project, schedule, late, 5, ExtensionStatus.Submitted, Me));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new DecideScheduleExtensionCommand(30, new ScheduleDecisionRequestDto { Approve = true }), default));

        f.Changes.GetExtensionAsync(30).Returns(ExtensionOf(project, schedule, late, 5, ExtensionStatus.Submitted, Other));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new DecideScheduleExtensionCommand(30, new ScheduleDecisionRequestDto { Approve = false }), default));
        await f.Changes.DidNotReceiveWithAnyArgs().SaveExtensionAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task Approval_moves_the_current_dates_keeps_the_originals_and_ends_the_delay()
    {
        var (f, project, schedule, late, next) = Setup(AppPermissions.ApproveScheduleExtension);
        var originalEnd = late.OriginalEnd;
        var oldEnd = late.PlannedEnd;
        var ext = ExtensionOf(project, schedule, late, 5, ExtensionStatus.Submitted, Other);
        var delay = DelayOf(project, late, DelayKind.Justified, DelayStatus.Approved, Other);
        f.Changes.GetExtensionAsync(30).Returns(ext);
        f.Changes.GetResolvableDelaysAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns([delay]);

        var dto = await f.Handler.Handle(new DecideScheduleExtensionCommand(30, new ScheduleDecisionRequestDto { Approve = true }), default);

        Assert.Equal(oldEnd.AddDays(5), late.PlannedEnd);                // الحالي تحرّك
        Assert.Equal(originalEnd, late.OriginalEnd);                      // الأصلي ثابت
        Assert.Equal(5, ScheduleRules.ExtraDays(late));
        Assert.Equal(Past.AddDays(10), next.PlannedEnd);                  // اللاحق لم يُمسّ (مقترح فقط)
        Assert.Equal(DelayStatus.Resolved, delay.Status);
        Assert.Equal(ExtensionStatus.Approved, ext.Status);
        Assert.Equal(Me, ext.DecidedByUserId);
        Assert.Equal("معتمد", dto.StatusName);
        await f.Changes.Received(1).SaveExtensionAsync(ext, Arg.Any<byte[]?>(), Arg.Is<AuditEntry>(a => a.Action == AuditActions.Approved), 1);
        await f.Notifications.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Notification>>(n =>
            n.Single().UserId == Other && n.Single().Type == NotificationType.ScheduleExtensionDecided));
    }

    [Fact]
    public async Task A_stale_request_is_not_approved_and_changes_no_date()
    {
        var (f, project, schedule, late, _) = Setup(AppPermissions.ApproveScheduleExtension);
        var ext = ExtensionOf(project, schedule, late, 5, ExtensionStatus.Submitted, Other);
        late.PlannedEnd = late.PlannedEnd.AddDays(2);                     // قرار آخر غيّر التاريخ بعد رفع الطلب
        var moved = late.PlannedEnd;
        f.Changes.GetExtensionAsync(30).Returns(ext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new DecideScheduleExtensionCommand(30, new ScheduleDecisionRequestDto { Approve = true }), default));

        Assert.Equal(moved, late.PlannedEnd);
        Assert.Equal(ExtensionStatus.Submitted, ext.Status);
        await f.Changes.DidNotReceiveWithAnyArgs().SaveExtensionAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task Rejection_changes_no_date_and_tells_the_requester()
    {
        var (f, project, schedule, late, _) = Setup(AppPermissions.ApproveScheduleExtension);
        var end = late.PlannedEnd;
        var ext = ExtensionOf(project, schedule, late, 5, ExtensionStatus.Submitted, Other);
        f.Changes.GetExtensionAsync(30).Returns(ext);

        await f.Handler.Handle(new DecideScheduleExtensionCommand(30, new ScheduleDecisionRequestDto { Approve = false, Note = "غير مبرر" }), default);

        Assert.Equal(ExtensionStatus.Rejected, ext.Status);
        Assert.Equal(end, late.PlannedEnd);
        await f.Changes.Received(1).SaveExtensionAsync(ext, Arg.Any<byte[]?>(), Arg.Is<AuditEntry>(a => a.Action == AuditActions.Rejected), 1);
    }

    [Fact]
    public async Task A_finished_project_accepts_no_delay_or_extension_work()
    {
        var (f, _, _, late, _) = Setup(AppPermissions.RecordScheduleDelay, AppPermissions.RequestScheduleExtension);
        var closed = ProjectOf(ProjectStage.Closed, Me, Other);
        var schedule = ApprovedOf(closed, late);
        f.Projects.GetByIdAsync(5).Returns(closed);
        f.Schedules.GetTaskAsync(TaskId).Returns(late);
        f.Schedules.GetApprovedAsync(5).Returns(schedule);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(
            new RecordScheduleDelayCommand(TaskId, new ScheduleDelayRequestDto { DiscoveredAt = DateTime.Today, Reason = "x" }), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(new CreateScheduleExtensionCommand(5, Extension()), default));
    }
}

/// <summary>تنبيه مرشّحي التأخر: لمن يملك تسجيل التأخير ويرى المشروع ولمسؤول البند، مرة لكل نهاية، ولا قرار</summary>
public class ScheduleOverdueReminderTests
{
    private static User UserOf(int id) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1, Role = new Role { Id = 1, Name = "Emp" }, BranchId = 1, DepartmentId = 10
    };

    private static ScheduleTask OverdueTask(Project project, int id, int? assignee = null)
    {
        var schedule = new ProjectSchedule { Id = 7, ProjectId = project.Id, Project = project, Status = ScheduleStatus.Approved };
        return new ScheduleTask
        {
            Id = id, ScheduleId = 7, Schedule = schedule, Name = "حفر", Status = ScheduleTaskStatus.InProgress,
            PlannedStart = DateTime.Today.AddDays(-10), PlannedEnd = DateTime.Today.AddDays(-2), AssigneeUserId = assignee
        };
    }

    [Fact]
    public async Task Holders_who_see_the_project_and_the_assignee_are_notified_once_per_end_date()
    {
        var project = new Project
        {
            Id = 5, Name = "مشروع", BranchId = 1, Stage = ProjectStage.Execution, CreatedAt = new DateTime(2026, 10, 27),
            Members = [new ProjectMember { ProjectId = 5, UserId = 3, User = UserOf(3) }, new ProjectMember { ProjectId = 5, UserId = 4, User = UserOf(4) }]
        };
        var task = OverdueTask(project, 1, assignee: 4);
        var changes = Substitute.For<IScheduleChangeService>();
        changes.GetOverdueToNotifyAsync(Arg.Any<DateTime>(), Arg.Any<int>()).Returns([task]);
        var permissions = Substitute.For<IUserPermissionService>();
        // حاملان للصلاحية: 3 يرى المشروع (مشارك) و8 لا يراه
        permissions.GetUsersWithPermissionAsync(AppPermissions.RecordScheduleDelay, Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>())
            .Returns([UserOf(3), UserOf(8)]);
        permissions.GetAsync(Arg.Any<int>()).Returns((IReadOnlySet<string>)new HashSet<string> { AppPermissions.ViewProjects });
        var notifications = Substitute.For<INotificationService>();

        var sent = await ScheduleOverdueReminders.RunAsync(changes, permissions, notifications, DateTime.Now);

        Assert.Equal(1, sent);
        await notifications.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Notification>>(n =>
            n.Select(x => x.UserId).OrderBy(x => x).SequenceEqual(new[] { 3, 4 }) && n.All(x => x.Type == NotificationType.ScheduleTaskOverdue)));
        Assert.Equal(task.PlannedEnd.Date, task.OverdueNotifiedFor);     // لن يتكرر لهذه النهاية
        await changes.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Nothing_is_sent_when_no_task_is_overdue()
    {
        var changes = Substitute.For<IScheduleChangeService>();
        changes.GetOverdueToNotifyAsync(Arg.Any<DateTime>(), Arg.Any<int>()).Returns([]);
        var notifications = Substitute.For<INotificationService>();

        Assert.Equal(0, await ScheduleOverdueReminders.RunAsync(changes, Substitute.For<IUserPermissionService>(), notifications, DateTime.Now));

        await notifications.DidNotReceiveWithAnyArgs().AddRangeAsync(default!);
        await changes.DidNotReceive().SaveChangesAsync();
    }
}
