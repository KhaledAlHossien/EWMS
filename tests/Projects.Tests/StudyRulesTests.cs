using Application.Common;
using Application.Features.Studies;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>
/// قواعد الدراسات والمكاتب الفنية: حدّها حدّ المشروع، المراجع غير المُعدّ حتى لمدير النظام، التسلسل يطبَّق عند اختياره فقط،
/// والإرسال للمركزية بعد اعتماد كل المخرجات، والنظام لا يقرر بمرور الوقت.
/// </summary>
public class StudyRulesTests
{
    private const int Branch = 1;
    private static readonly DateTime Today = new(2026, 10, 28);

    private static User UserOf(int id, string role = "Emp", int? office = null, bool active = true) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1, IsActive = active,
        Role = new Role { Id = 1, Name = role }, BranchId = Branch, DepartmentId = 10, OfficeId = office
    };

    private static Viewer ViewerOf(User user, params string[] permissions) =>
        new(user, new HashSet<string>(permissions.Append(AppPermissions.ViewProjects)));

    private static Project ProjectOf(ProjectStage stage = ProjectStage.Study, params User[] members) => new()
    {
        Id = 1, Name = "p", BranchId = Branch, Stage = stage, CreatedByUserId = 99,
        Members = members.Select(m => new ProjectMember { ProjectId = 1, UserId = m.Id, User = m }).ToList()
    };

    private static DeliverableType TypeOf(int id, string name, int branch = Branch, bool active = true) =>
        new() { Id = id, BranchId = branch, Name = name, IsActive = active };

    private static StudyDeliverable Deliverable(int id, int sort, DeliverableState state = DeliverableState.Preparing, int? assignee = null) => new()
    {
        Id = id, TypeId = id, Type = TypeOf(id, "نوع " + id), SortOrder = sort, State = state, AssigneeUserId = assignee
    };

    private static StudyOrder OrderOf(Project project, StudyMode mode = StudyMode.Sequential, StudyStatus status = StudyStatus.Open, params StudyDeliverable[] deliverables)
    {
        var order = new StudyOrder { Id = 7, ProjectId = project.Id, Project = project, Mode = mode, Status = status, Title = "دراسة", Deliverables = deliverables.ToList() };
        foreach (var d in deliverables) d.StudyOrder = order;
        return order;
    }

    // ───────── الأسماء والتحقق ─────────

    [Fact]
    public void The_number_follows_the_creation_year_and_pads_the_id()
    {
        Assert.Equal("STD-2026-00042", StudyRules.Number(42, new DateTime(2026, 3, 1)));
    }

    [Fact]
    public void Order_data_needs_a_title_and_a_sane_due_date()
    {
        Assert.NotNull(StudyRules.ValidateOrder("  ", null, null, Today));
        Assert.NotNull(StudyRules.ValidateOrder(new string('أ', 201), null, null, Today));
        Assert.NotNull(StudyRules.ValidateOrder("دراسة", null, Today.AddYears(-2), Today));
        Assert.Null(StudyRules.ValidateOrder("دراسة", "ملاحظة", Today.AddDays(30), Today));
        Assert.Null(StudyRules.ValidateOrder("دراسة", null, null, Today));
    }

    [Fact]
    public void An_assignee_must_be_an_active_member_of_the_project_and_belong_to_the_chosen_office()
    {
        var engineer = UserOf(5, office: 3);
        var project = ProjectOf(members: engineer);
        var office = new Office { Id = 3, DepartmentId = 10, Department = new Department { Id = 10, BranchId = Branch } };

        Assert.Null(StudyRules.ValidateAssignment(project, engineer, office));
        Assert.Null(StudyRules.ValidateAssignment(project, null, office));
        Assert.Null(StudyRules.ValidateAssignment(project, engineer, null));

        Assert.NotNull(StudyRules.ValidateAssignment(project, UserOf(6), null));                                   // ليس مشاركاً
        Assert.NotNull(StudyRules.ValidateAssignment(ProjectOf(members: UserOf(8, active: false)), UserOf(8, active: false), null));
        Assert.NotNull(StudyRules.ValidateAssignment(project, engineer, new Office { Id = 4, DepartmentId = 11, Department = new Department { Id = 11, BranchId = 2 } }));   // مكتب فرع آخر
        Assert.NotNull(StudyRules.ValidateAssignment(project, engineer, new Office { Id = 4, DepartmentId = 10, Department = new Department { Id = 10, BranchId = Branch } }));   // المهندس من مكتب آخر
    }

    // ───────── الرؤية والإنشاء ─────────

    [Fact]
    public void Viewing_needs_a_study_permission_and_a_visible_project()
    {
        var me = UserOf(1);
        var project = ProjectOf(members: me);

        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanView(ViewerOf(me), project));                       // بلا صلاحية دراسات
        StudyRules.EnsureCanView(ViewerOf(me, AppPermissions.ViewStudies), project);
        StudyRules.EnsureCanView(ViewerOf(me, AppPermissions.PrepareStudyDeliverable), project);                                  // من يعدّ يرى

        var outsider = UserOf(2);
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanView(ViewerOf(outsider, AppPermissions.ViewStudies), project));   // خارج حدّ المشروع
    }

    [Fact]
    public void Opening_an_order_needs_its_permission_and_an_unfinished_project()
    {
        var me = UserOf(1);
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanCreate(ViewerOf(me, AppPermissions.ViewStudies), ProjectOf(members: me)));
        StudyRules.EnsureCanCreate(ViewerOf(me, AppPermissions.CreateStudyOrder), ProjectOf(members: me));
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanCreate(ViewerOf(me, AppPermissions.CreateStudyOrder), ProjectOf(ProjectStage.Cancelled, me)));
    }

    [Fact]
    public void Editing_an_order_needs_the_permission_and_an_open_order()
    {
        var me = UserOf(1);
        var project = ProjectOf(members: me);
        StudyRules.EnsureCanEdit(ViewerOf(me, AppPermissions.EditStudyOrder), OrderOf(project));

        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanEdit(ViewerOf(me, AppPermissions.CreateStudyOrder), OrderOf(project)));
        foreach (var status in new[] { StudyStatus.SentToCenter, StudyStatus.ApprovedByCenter, StudyStatus.ReturnedByCenter, StudyStatus.Cancelled })
            Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanEdit(ViewerOf(me, AppPermissions.EditStudyOrder), OrderOf(project, status: status)));
    }

    [Fact]
    public void A_deliverable_type_is_added_once_per_order_and_must_be_active_and_in_the_branch()
    {
        var order = OrderOf(ProjectOf(), deliverables: Deliverable(1, 1));
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanAddDeliverable(order, typeId: 1));
        StudyRules.EnsureCanAddDeliverable(order, typeId: 2);

        StudyRules.EnsureTypeUsable(TypeOf(2, "إنشائي"), Branch);
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureTypeUsable(TypeOf(2, "إنشائي", active: false), Branch));
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureTypeUsable(TypeOf(2, "إنشائي", branch: 2), Branch));
    }

    [Fact]
    public void Only_an_untouched_deliverable_can_be_removed()
    {
        StudyRules.EnsureCanRemoveDeliverable(Deliverable(1, 1));
        var withFile = Deliverable(2, 2);
        withFile.DocumentId = 5;
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanRemoveDeliverable(withFile));
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanRemoveDeliverable(Deliverable(3, 3, DeliverableState.InReview)));
    }

    // ───────── الترتيب ─────────

    [Fact]
    public void In_a_sequential_order_a_deliverable_starts_only_after_everything_before_it_is_approved()
    {
        var architectural = Deliverable(1, 1, DeliverableState.Approved);
        var structural = Deliverable(2, 2);
        var electrical = Deliverable(3, 3);
        var order = OrderOf(ProjectOf(), StudyMode.Sequential, deliverables: [architectural, structural, electrical]);

        Assert.True(StudyRules.CanStart(order, architectural));
        Assert.True(StudyRules.CanStart(order, structural));
        Assert.False(StudyRules.CanStart(order, electrical));
        Assert.Same(structural, StudyRules.BlockedBy(order, electrical));
        Assert.Null(StudyRules.BlockedBy(order, structural));
    }

    [Fact]
    public void In_a_parallel_order_everything_can_start()
    {
        var order = OrderOf(ProjectOf(), StudyMode.Parallel, deliverables: [Deliverable(1, 1), Deliverable(2, 2), Deliverable(3, 3)]);
        Assert.All(order.Deliverables, d => Assert.True(StudyRules.CanStart(order, d)));
        Assert.All(order.Deliverables, d => Assert.Null(StudyRules.BlockedBy(order, d)));
    }

    [Fact]
    public void Equal_sort_positions_are_ordered_by_id_so_the_sequence_is_stable()
    {
        var first = Deliverable(1, 5);
        var second = Deliverable(2, 5);
        var order = OrderOf(ProjectOf(), StudyMode.Sequential, deliverables: [second, first]);
        Assert.True(StudyRules.CanStart(order, first));
        Assert.False(StudyRules.CanStart(order, second));
    }

    // ───────── الإعداد ─────────

    private static (Viewer Preparer, StudyOrder Order, StudyDeliverable D) PrepareSetup(DeliverableState state = DeliverableState.Preparing,
        StudyStatus status = StudyStatus.Open, StudyMode mode = StudyMode.Parallel)
    {
        var engineer = UserOf(5);
        var d = Deliverable(1, 1, state, assignee: engineer.Id);
        var order = OrderOf(ProjectOf(members: engineer), mode, status, d);
        return (ViewerOf(engineer, AppPermissions.PrepareStudyDeliverable), order, d);
    }

    [Fact]
    public void The_assignee_prepares_a_deliverable_in_an_open_order()
    {
        var (viewer, order, d) = PrepareSetup();
        StudyRules.EnsureCanPrepare(viewer, order, d);
        StudyRules.EnsureCanPrepare(viewer, order, Deliverable(9, 9, DeliverableState.Returned, assignee: viewer.Id));
    }

    [Fact]
    public void Someone_else_cannot_prepare_a_deliverable_even_with_the_permission()
    {
        var (_, order, d) = PrepareSetup();
        var other = UserOf(6);
        order.Project.Members.Add(new ProjectMember { ProjectId = 1, UserId = 6, User = other });
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanPrepare(ViewerOf(other, AppPermissions.PrepareStudyDeliverable), order, d));
    }

    [Fact]
    public void Preparing_needs_the_permission()
    {
        var (viewer, order, d) = PrepareSetup();
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanPrepare(ViewerOf(viewer.User, AppPermissions.ViewStudies), order, d));
    }

    [Theory]
    [InlineData(DeliverableState.InReview)]
    [InlineData(DeliverableState.Approved)]
    public void A_frozen_or_approved_deliverable_is_not_prepared(DeliverableState state)
    {
        var (viewer, order, d) = PrepareSetup(state);
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanPrepare(viewer, order, d));
    }

    [Theory]
    [InlineData(StudyStatus.SentToCenter)]
    [InlineData(StudyStatus.ApprovedByCenter)]
    [InlineData(StudyStatus.ReturnedByCenter)]
    [InlineData(StudyStatus.Cancelled)]
    public void Preparing_is_refused_unless_the_order_is_open(StudyStatus status)
    {
        var (viewer, order, d) = PrepareSetup(status: status);
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanPrepare(viewer, order, d));
    }

    [Fact]
    public void A_deliverable_waiting_for_an_earlier_one_cannot_be_prepared_in_a_sequential_order()
    {
        var engineer = UserOf(5);
        var first = Deliverable(1, 1);
        var second = Deliverable(2, 2, assignee: engineer.Id);
        var order = OrderOf(ProjectOf(members: engineer), StudyMode.Sequential, deliverables: [first, second]);
        var viewer = ViewerOf(engineer, AppPermissions.PrepareStudyDeliverable);

        var ex = Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanPrepare(viewer, order, second));
        Assert.Contains("نوع 1", ex.Message);
        first.State = DeliverableState.Approved;
        StudyRules.EnsureCanPrepare(viewer, order, second);
    }

    [Fact]
    public void A_finished_project_freezes_preparation()
    {
        var engineer = UserOf(5);
        var d = Deliverable(1, 1, assignee: engineer.Id);
        var order = OrderOf(ProjectOf(ProjectStage.Closed, engineer), StudyMode.Parallel, deliverables: d);
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanPrepare(ViewerOf(engineer, AppPermissions.PrepareStudyDeliverable), order, d));
    }

    [Fact]
    public void Submitting_for_review_needs_an_assignee_and_a_file()
    {
        var (viewer, order, d) = PrepareSetup();
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanSubmitForReview(viewer, order, d, latestVersionId: null));
        StudyRules.EnsureCanSubmitForReview(viewer, order, d, latestVersionId: 11);

        d.AssigneeUserId = null;
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanSubmitForReview(viewer, order, d, 11));   // لا مسند إليه فلا يُعدّ بغير مدير النظام
    }

    [Fact]
    public void After_a_return_a_new_version_is_needed_before_resubmitting()
    {
        var (viewer, order, d) = PrepareSetup(DeliverableState.Returned);
        d.SubmittedVersionId = 11;
        var ex = Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanSubmitForReview(viewer, order, d, latestVersionId: 11));
        Assert.Contains("إصداراً جديداً", ex.Message);
        StudyRules.EnsureCanSubmitForReview(viewer, order, d, latestVersionId: 12);
    }

    // ───────── المراجعة ─────────

    private static (Viewer Reviewer, StudyOrder Order, StudyDeliverable D) ReviewSetup(int submitter = 5, int reviewerId = 6)
    {
        var reviewer = UserOf(reviewerId);
        var d = Deliverable(1, 1, DeliverableState.InReview, assignee: submitter);
        d.SubmittedByUserId = submitter;
        var order = OrderOf(ProjectOf(members: [UserOf(submitter), reviewer]), StudyMode.Parallel, deliverables: d);
        return (ViewerOf(reviewer, AppPermissions.ReviewStudyDeliverable), order, d);
    }

    [Fact]
    public void Someone_other_than_the_preparer_approves_or_returns_with_a_reason()
    {
        var (viewer, order, d) = ReviewSetup();
        StudyRules.EnsureCanReview(viewer, order, d, submittedVersionUploaderId: 5, approve: true, note: null);
        StudyRules.EnsureCanReview(viewer, order, d, 5, approve: false, note: "عدّل المقياس");
    }

    [Fact]
    public void Returning_a_deliverable_needs_a_reason()
    {
        var (viewer, order, d) = ReviewSetup();
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanReview(viewer, order, d, 5, approve: false, note: "  "));
    }

    [Fact]
    public void The_one_who_submitted_never_reviews_even_a_super_admin()
    {
        var (_, order, d) = ReviewSetup();
        var submitter = ViewerOf(UserOf(5), AppPermissions.ReviewStudyDeliverable);
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanReview(submitter, order, d, 5, true, null));

        var admin = ViewerOf(UserOf(5, role: "SuperAdmin"));
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanReview(admin, order, d, 5, true, null));
    }

    [Fact]
    public void The_one_who_uploaded_the_submitted_file_does_not_review_it()
    {
        var (viewer, order, d) = ReviewSetup();
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanReview(viewer, order, d, submittedVersionUploaderId: viewer.Id, true, null));
    }

    [Fact]
    public void Reviewing_needs_its_permission_and_a_deliverable_in_review()
    {
        var (viewer, order, d) = ReviewSetup();
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanReview(ViewerOf(viewer.User, AppPermissions.ViewStudies), order, d, 5, true, null));

        d.State = DeliverableState.Preparing;
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanReview(viewer, order, d, 5, true, null));
    }

    [Fact]
    public void Sending_an_approved_deliverable_back_needs_the_edit_permission_a_reason_and_an_open_order()
    {
        var head = UserOf(1);
        var d = Deliverable(1, 1, DeliverableState.Approved);
        var order = OrderOf(ProjectOf(members: head), StudyMode.Parallel, deliverables: d);
        var viewer = ViewerOf(head, AppPermissions.EditStudyOrder);

        StudyRules.EnsureCanSendBack(viewer, order, d, "تغيّر المقياس");
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanSendBack(viewer, order, d, " "));
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanSendBack(ViewerOf(head, AppPermissions.ViewStudies), order, d, "سبب"));
        d.State = DeliverableState.Preparing;
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanSendBack(viewer, order, d, "سبب"));
    }

    // ───────── الإرسال للمركزية وقرارها ─────────

    private static (Viewer Sender, StudyOrder Order) SendSetup(params StudyDeliverable[] deliverables)
    {
        var me = UserOf(1);
        return (ViewerOf(me, AppPermissions.RecordStudySubmission), OrderOf(ProjectOf(members: me), StudyMode.Parallel, StudyStatus.Open, deliverables));
    }

    [Fact]
    public void An_order_is_sent_only_when_every_deliverable_is_approved()
    {
        var (viewer, order) = SendSetup(Deliverable(1, 1, DeliverableState.Approved), Deliverable(2, 2, DeliverableState.Approved));
        StudyRules.EnsureCanSend(viewer, order);

        order.Deliverables.Last().State = DeliverableState.InReview;
        var ex = Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanSend(viewer, order));
        Assert.Contains("اعتماد كل مخرجاتها", ex.Message);
    }

    [Fact]
    public void An_order_without_deliverables_is_not_sent()
    {
        var (viewer, order) = SendSetup();
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanSend(viewer, order));
    }

    [Fact]
    public void Sending_needs_the_permission_and_waits_for_a_pending_decision()
    {
        var (viewer, order) = SendSetup(Deliverable(1, 1, DeliverableState.Approved));
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanSend(ViewerOf(viewer.User, AppPermissions.ViewStudies), order));

        order.Submissions.Add(new StudySubmission { Round = 1, Decision = CenterDecision.Pending });
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanSend(viewer, order));
    }

    [Fact]
    public void Rounds_count_up_from_one()
    {
        var (_, order) = SendSetup();
        Assert.Equal(1, StudyRules.NextRound(order));
        order.Submissions.Add(new StudySubmission { Round = 1, Decision = CenterDecision.Notes });
        order.Submissions.Add(new StudySubmission { Round = 2, Decision = CenterDecision.Notes });
        Assert.Equal(3, StudyRules.NextRound(order));
    }

    private static (Viewer Recorder, StudyOrder Order, StudySubmission Submission) DecideSetup()
    {
        var me = UserOf(1);
        var submission = new StudySubmission { Round = 1, Decision = CenterDecision.Pending };
        var order = OrderOf(ProjectOf(members: me), StudyMode.Parallel, StudyStatus.SentToCenter);
        order.Submissions.Add(submission);
        return (ViewerOf(me, AppPermissions.RecordStudySubmission), order, submission);
    }

    [Fact]
    public void The_center_decision_is_approved_or_notes_and_notes_need_text()
    {
        var (viewer, order, submission) = DecideSetup();
        StudyRules.EnsureCanDecide(viewer, order, submission, CenterDecision.Approved, null);
        StudyRules.EnsureCanDecide(viewer, order, submission, CenterDecision.Notes, "راجعوا الكميات");
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanDecide(viewer, order, submission, CenterDecision.Notes, ""));
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanDecide(viewer, order, submission, CenterDecision.Pending, null));
    }

    [Fact]
    public void A_decision_is_recorded_once_and_only_for_an_order_awaiting_the_center()
    {
        var (viewer, order, submission) = DecideSetup();
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanDecide(ViewerOf(viewer.User, AppPermissions.ViewStudies), order, submission, CenterDecision.Approved, null));

        submission.Decision = CenterDecision.Approved;
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanDecide(viewer, order, submission, CenterDecision.Approved, null));

        submission.Decision = CenterDecision.Pending;
        order.Status = StudyStatus.Open;
        Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanDecide(viewer, order, submission, CenterDecision.Approved, null));
    }

    [Fact]
    public void The_decision_sets_the_order_status()
    {
        Assert.Equal(StudyStatus.ApprovedByCenter, StudyRules.StatusAfter(CenterDecision.Approved));
        Assert.Equal(StudyStatus.ReturnedByCenter, StudyRules.StatusAfter(CenterDecision.Notes));
    }

    [Fact]
    public void Only_an_order_returned_by_the_center_is_reopened_for_a_new_round()
    {
        var me = UserOf(1);
        var viewer = ViewerOf(me, AppPermissions.EditStudyOrder);
        var project = ProjectOf(members: me);
        StudyRules.EnsureCanReopen(viewer, OrderOf(project, status: StudyStatus.ReturnedByCenter));
        foreach (var status in new[] { StudyStatus.Open, StudyStatus.SentToCenter, StudyStatus.ApprovedByCenter, StudyStatus.Cancelled })
            Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanReopen(viewer, OrderOf(project, status: status)));
        Assert.Throws<UnauthorizedAccessException>(() => StudyRules.EnsureCanReopen(ViewerOf(me, AppPermissions.ViewStudies), OrderOf(project, status: StudyStatus.ReturnedByCenter)));
    }

    [Fact]
    public void Cancelling_is_refused_while_waiting_for_the_center_after_its_approval_or_twice()
    {
        var me = UserOf(1);
        var viewer = ViewerOf(me, AppPermissions.EditStudyOrder);
        var project = ProjectOf(members: me);
        StudyRules.EnsureCanCancel(viewer, OrderOf(project, status: StudyStatus.Open));
        StudyRules.EnsureCanCancel(viewer, OrderOf(project, status: StudyStatus.ReturnedByCenter));
        foreach (var status in new[] { StudyStatus.SentToCenter, StudyStatus.ApprovedByCenter, StudyStatus.Cancelled })
            Assert.Throws<InvalidOperationException>(() => StudyRules.EnsureCanCancel(viewer, OrderOf(project, status: status)));
    }

    // ───────── التنبيه: لا قرار ─────────

    [Fact]
    public void An_overdue_deliverable_is_only_a_notice_candidate_in_an_open_order()
    {
        var late = Deliverable(1, 1);
        late.DueDate = Today.AddDays(-1);
        var order = OrderOf(ProjectOf(), deliverables: late);

        Assert.True(StudyRules.IsOverdueCandidate(order, late, Today));
        late.DueDate = Today;                                                   // اليوم نفسه ليس تأخراً
        Assert.False(StudyRules.IsOverdueCandidate(order, late, Today));
        late.DueDate = Today.AddDays(-1);
        late.State = DeliverableState.Approved;
        Assert.False(StudyRules.IsOverdueCandidate(order, late, Today));        // المعتمد لا يُنبَّه عنه
        late.State = DeliverableState.Preparing;
        order.Status = StudyStatus.SentToCenter;
        Assert.False(StudyRules.IsOverdueCandidate(order, late, Today));
    }

    [Fact]
    public void The_overdue_notice_is_once_per_due_date()
    {
        var d = Deliverable(1, 1);
        d.DueDate = new DateTime(2026, 10, 20);
        Assert.True(StudyRules.NeedsOverdueNotice(d));
        d.OverdueNotifiedFor = new DateTime(2026, 10, 20);
        Assert.False(StudyRules.NeedsOverdueNotice(d));
        d.DueDate = new DateTime(2026, 10, 27);                                // موعد جديد: تنبيه جديد
        Assert.True(StudyRules.NeedsOverdueNotice(d));
        d.DueDate = null;
        Assert.False(StudyRules.NeedsOverdueNotice(d));
    }

    [Fact]
    public void The_approved_percent_counts_only_approved_deliverables()
    {
        Assert.Equal(0, StudyRules.ApprovedPercent(OrderOf(ProjectOf())));
        var order = OrderOf(ProjectOf(), deliverables: [Deliverable(1, 1, DeliverableState.Approved), Deliverable(2, 2), Deliverable(3, 3, DeliverableState.InReview)]);
        Assert.Equal(33, StudyRules.ApprovedPercent(order));
    }

    [Fact]
    public void Arabic_labels_exist_for_every_member_of_the_enums()
    {
        foreach (var s in Enum.GetValues<StudyStatus>()) Assert.DoesNotMatch("^[A-Za-z]+$", StudyRules.StatusAr(s));
        foreach (var s in Enum.GetValues<DeliverableState>()) Assert.DoesNotMatch("^[A-Za-z]+$", StudyRules.StateAr(s));
        foreach (var s in Enum.GetValues<CenterDecision>()) Assert.DoesNotMatch("^[A-Za-z]+$", StudyRules.DecisionAr(s));
        Assert.Equal("بالتوازي", StudyRules.ModeAr(StudyMode.Parallel));
        Assert.Equal("بالتسلسل", StudyRules.ModeAr(StudyMode.Sequential));
        Assert.True(StudyRules.IsValidMode(1));
        Assert.False(StudyRules.IsValidMode(3));
    }
}
