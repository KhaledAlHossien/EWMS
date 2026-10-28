using Application.Common;
using Application.Features.Diwan;
using Domain.Entities;

namespace Projects.Tests.Diwan;

/// <summary>
/// قواعد الديوان الصرفة (المرحلة 2): الترقيم والمواعيد، حدود الرؤية (والإحالة لا تفتح المعاملة لكل القسم)، من يعالج ويحيل،
/// وتغيير الحالة والإغلاق، ومسار الصادر: مسودة ← اعتماد (غير مُعدّها) ← صدور.
/// </summary>
public class DiwanRulesTests
{
    private const int Branch = 1, Studies = 10, StudiesOffice = 100, Survey = 11, OtherBranch = 2;
    private const int Registrar = 1, Handler = 2, Outsider = 3, Approver = 4, Referred = 5;

    private static User UserOf(int id, int? branch = Branch, int? department = Studies, int? office = null, string role = "Emp") => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = department, OfficeId = office
    };

    private static Viewer ViewerOf(User user, params string[] permissions) => new(user, new HashSet<string>(permissions));

    private static CorrespondenceStatus StatusOf(CorrespondenceStage stage, int branch = Branch, bool active = true) =>
        new() { Id = (int)stage, BranchId = branch, Name = stage.ToString(), Stage = stage, IsActive = active };

    private static Correspondence Incoming(CorrespondenceStage stage = CorrespondenceStage.Registered, int department = Studies, int? office = null, int? user = null) => new()
    {
        Id = 1, Direction = CorrespondenceDirection.Incoming, BranchId = Branch, DepartmentId = 50, Year = 2026, Sequence = 12,
        Party = "المركزية", Subject = "أمر دراسة", TypeId = 1, RegisteredByUserId = Registrar, RegisteredAt = new DateTime(2026, 11, 1),
        Status = StatusOf(stage), StatusId = (int)stage, CurrentDepartmentId = department, CurrentOfficeId = office, CurrentUserId = user
    };

    private static Correspondence Outgoing(OutgoingState state = OutgoingState.Draft, int registeredBy = Registrar) => new()
    {
        Id = 2, Direction = CorrespondenceDirection.Outgoing, OutgoingState = state, BranchId = Branch, DepartmentId = 50, Year = 2026,
        Party = "المركزية", Subject = "إرسال الدراسة", TypeId = 1, RegisteredByUserId = registeredBy, RegisteredAt = new DateTime(2026, 11, 1)
    };

    // ───────── الأرقام والمواعيد ─────────

    [Fact]
    public void The_number_uses_the_direction_year_and_five_digits_and_a_draft_has_none()
    {
        Assert.Equal("IN-2026-00012", DiwanRules.Number(CorrespondenceDirection.Incoming, 2026, 12));
        Assert.Equal("OUT-2027-00003", DiwanRules.Number(CorrespondenceDirection.Outgoing, 2027, 3));
        Assert.Null(DiwanRules.Number(CorrespondenceDirection.Outgoing, 2026, null));
    }

    [Fact]
    public void The_due_date_is_registration_day_plus_the_type_deadline_or_none()
    {
        Assert.Equal(new DateTime(2026, 11, 11), DiwanRules.DueDate(new DateTime(2026, 11, 1, 15, 30, 0), 10));
        Assert.Null(DiwanRules.DueDate(new DateTime(2026, 11, 1), null));
        Assert.Null(DiwanRules.DueDate(new DateTime(2026, 11, 1), 0));
    }

    [Fact]
    public void An_open_incoming_past_its_due_date_is_only_a_candidate_for_delay()
    {
        var today = new DateTime(2026, 11, 12);
        var open = Incoming(CorrespondenceStage.InProgress); open.DueDate = new DateTime(2026, 11, 11);
        Assert.True(DiwanRules.IsOverdueCandidate(open, today));

        open.DueDate = new DateTime(2026, 11, 12);   // اليوم نفسه ليس تأخراً
        Assert.False(DiwanRules.IsOverdueCandidate(open, today));
    }

    [Theory]
    [InlineData(CorrespondenceStage.Answered)]
    [InlineData(CorrespondenceStage.Closed)]
    public void An_answered_or_closed_incoming_is_never_overdue(CorrespondenceStage stage)
    {
        var c = Incoming(stage); c.DueDate = new DateTime(2026, 1, 1);
        Assert.False(DiwanRules.IsOverdueCandidate(c, new DateTime(2026, 11, 12)));
        Assert.False(DiwanRules.IsDueSoon(c, new DateTime(2026, 1, 1)));
    }

    [Fact]
    public void Due_soon_means_today_or_tomorrow_and_outgoing_has_no_deadline()
    {
        var c = Incoming(); c.DueDate = new DateTime(2026, 11, 13);
        Assert.True(DiwanRules.IsDueSoon(c, new DateTime(2026, 11, 12)));
        Assert.True(DiwanRules.IsDueSoon(c, new DateTime(2026, 11, 13)));
        Assert.False(DiwanRules.IsDueSoon(c, new DateTime(2026, 11, 11)));

        var outgoing = Outgoing(); outgoing.DueDate = new DateTime(2026, 11, 13);
        Assert.False(DiwanRules.IsDueSoon(outgoing, new DateTime(2026, 11, 12)));
        Assert.False(DiwanRules.IsOverdueCandidate(outgoing, new DateTime(2026, 12, 1)));
    }

    // ───────── التحقق ─────────

    [Fact]
    public void Registration_needs_a_subject_and_a_party_within_their_limits()
    {
        Assert.Null(DiwanRules.ValidateRegistration("المركزية", "أمر دراسة", null, null));
        Assert.NotNull(DiwanRules.ValidateRegistration("المركزية", " ", null, null));
        Assert.NotNull(DiwanRules.ValidateRegistration(" ", "موضوع", null, null));
        Assert.NotNull(DiwanRules.ValidateRegistration("ج", new string('م', 301), null, null));
        Assert.NotNull(DiwanRules.ValidateRegistration(new string('ج', 201), "م", null, null));
        Assert.NotNull(DiwanRules.ValidateRegistration("ج", "م", new string('ن', 4001), null));
        Assert.NotNull(DiwanRules.ValidateRegistration("ج", "م", null, new string('1', 51)));
    }

    [Fact]
    public void Types_and_statuses_are_validated()
    {
        Assert.Null(DiwanRules.ValidateType("أمر دراسة", 14));
        Assert.Null(DiwanRules.ValidateType("إعلام", null));
        Assert.NotNull(DiwanRules.ValidateType("", 5));
        Assert.NotNull(DiwanRules.ValidateType("نوع", 0));
        Assert.NotNull(DiwanRules.ValidateType("نوع", DiwanRules.MaxTypeDeadlineDays + 1));

        Assert.Null(DiwanRules.ValidateStatus("قيد الدراسة", (int)CorrespondenceStage.InProgress, "#34C759"));
        Assert.NotNull(DiwanRules.ValidateStatus("", 1, "#34C759"));
        Assert.NotNull(DiwanRules.ValidateStatus("ح", 9, "#34C759"));
        Assert.NotNull(DiwanRules.ValidateStatus("ح", 1, "green"));
    }

    // ───────── الرؤية ─────────

    [Fact]
    public void Someone_without_any_correspondence_permission_sees_nothing()
    {
        Assert.False(DiwanRules.CanView(ViewerOf(UserOf(Outsider)), Incoming()));
    }

    [Fact]
    public void The_registrar_sees_what_they_registered_even_after_it_was_referred_away()
    {
        var c = Incoming(department: Survey);
        Assert.True(DiwanRules.CanView(ViewerOf(UserOf(Registrar, department: 50), AppPermissions.RegisterCorrespondence), c));
    }

    [Fact]
    public void Referring_to_a_department_does_not_open_the_item_to_all_its_staff()
    {
        var c = Incoming(department: Studies);
        var plainStaff = ViewerOf(UserOf(Outsider, department: Studies), AppPermissions.ViewCorrespondence);
        var holder = ViewerOf(UserOf(Handler, department: Studies), AppPermissions.HandleCorrespondence);

        Assert.False(DiwanRules.CanView(plainStaff, c));      // قاعدة 8
        Assert.True(DiwanRules.CanView(holder, c));
    }

    [Fact]
    public void A_user_named_in_a_referral_or_as_the_current_holder_sees_it()
    {
        var c = Incoming(department: Studies);
        c.Referrals.Add(new CorrespondenceReferral { Id = 1, FromUserId = Registrar, ToDepartmentId = Studies, ToUserId = Referred, CreatedAt = new DateTime(2026, 11, 2) });

        Assert.True(DiwanRules.CanView(ViewerOf(UserOf(Referred, department: Survey), AppPermissions.ViewCorrespondence), c));
        Assert.True(DiwanRules.CanView(ViewerOf(UserOf(Registrar, department: 50), AppPermissions.ViewCorrespondence), c));
        Assert.False(DiwanRules.CanView(ViewerOf(UserOf(Outsider, department: Survey), AppPermissions.ViewCorrespondence), c));
    }

    [Fact]
    public void The_office_holder_sees_office_items_and_a_department_level_holder_does_not()
    {
        var c = Incoming(department: Studies, office: StudiesOffice);
        Assert.True(DiwanRules.CanView(ViewerOf(UserOf(Handler, office: StudiesOffice), AppPermissions.HandleCorrespondence), c));
        Assert.False(DiwanRules.CanView(ViewerOf(UserOf(Outsider, office: null), AppPermissions.HandleCorrespondence), c));
        Assert.False(DiwanRules.CanView(ViewerOf(UserOf(Outsider, office: 101), AppPermissions.HandleCorrespondence), c));
    }

    [Fact]
    public void The_department_permission_covers_items_registered_held_or_passed_through_by_the_department()
    {
        var viewer = ViewerOf(UserOf(Outsider, department: Survey), AppPermissions.ViewDepartmentCorrespondence);

        Assert.False(DiwanRules.CanView(viewer, Incoming(department: Studies)));

        var held = Incoming(department: Survey);
        Assert.True(DiwanRules.CanView(viewer, held));

        var passedThrough = Incoming(department: Studies);
        passedThrough.Referrals.Add(new CorrespondenceReferral { Id = 1, FromUserId = Registrar, FromDepartmentId = Survey, ToDepartmentId = Studies });
        Assert.True(DiwanRules.CanView(viewer, passedThrough));

        var registeredHere = Incoming(department: Studies); registeredHere.DepartmentId = Survey;
        Assert.True(DiwanRules.CanView(viewer, registeredHere));
    }

    [Fact]
    public void The_branch_permission_covers_the_branch_only()
    {
        var viewer = ViewerOf(UserOf(Outsider, branch: Branch, department: Survey), AppPermissions.ViewBranchCorrespondence);
        var other = Incoming(); other.BranchId = OtherBranch;

        Assert.True(DiwanRules.CanView(viewer, Incoming(department: Studies)));
        Assert.False(DiwanRules.CanView(viewer, other));
    }

    [Fact]
    public void A_user_with_no_unit_matches_no_record_even_when_the_record_has_none()
    {
        var c = Incoming(); c.DepartmentId = null; c.CurrentDepartmentId = null; c.CurrentOfficeId = null;
        var viewer = ViewerOf(UserOf(Outsider, branch: null, department: null),
            AppPermissions.ViewDepartmentCorrespondence, AppPermissions.HandleCorrespondence, AppPermissions.ViewBranchCorrespondence);

        Assert.False(DiwanRules.CanView(viewer, c));
    }

    [Fact]
    public void An_approver_sees_the_outgoing_drafts_of_the_branch_but_not_its_incoming()
    {
        var viewer = ViewerOf(UserOf(Approver, department: Survey), AppPermissions.ApproveOutgoingCorrespondence);

        Assert.True(DiwanRules.CanView(viewer, Outgoing()));
        Assert.False(DiwanRules.CanView(viewer, Incoming(department: Studies)));
    }

    [Fact]
    public void Organization_wide_viewers_and_the_super_admin_see_everything()
    {
        var c = Incoming(); c.BranchId = OtherBranch;
        Assert.True(DiwanRules.CanView(ViewerOf(UserOf(Outsider), AppPermissions.ViewOrganizationDashboard), c));
        Assert.True(DiwanRules.CanView(ViewerOf(UserOf(Outsider, branch: null, department: null, role: "SuperAdmin")), c));
    }

    // ───────── من يعالج ويحيل ─────────

    [Fact]
    public void Only_a_holder_of_the_current_unit_handles_an_item()
    {
        var c = Incoming(department: Studies);
        Assert.True(DiwanRules.CanHandle(ViewerOf(UserOf(Handler, department: Studies), AppPermissions.HandleCorrespondence), c));
        Assert.False(DiwanRules.CanHandle(ViewerOf(UserOf(Handler, department: Studies)), c), "دون الصلاحية");
        Assert.False(DiwanRules.CanHandle(ViewerOf(UserOf(Handler, department: Survey), AppPermissions.HandleCorrespondence), c), "وحدة أخرى");
        Assert.False(DiwanRules.CanHandle(ViewerOf(UserOf(Handler, department: Studies, office: StudiesOffice), AppPermissions.HandleCorrespondence), c), "مكتب داخل القسم لا يعالج معاملة القسم نفسه");
    }

    [Fact]
    public void A_named_user_handles_the_item_wherever_they_work()
    {
        var c = Incoming(department: Studies, user: Referred);
        Assert.True(DiwanRules.CanHandle(ViewerOf(UserOf(Referred, department: Survey), AppPermissions.HandleCorrespondence), c));
        Assert.False(DiwanRules.CanHandle(ViewerOf(UserOf(Referred, department: Survey)), c));
    }

    [Fact]
    public void Referral_needs_the_permission_a_held_open_incoming_item()
    {
        var holder = ViewerOf(UserOf(Handler), AppPermissions.HandleCorrespondence, AppPermissions.ReferCorrespondence);
        DiwanRules.EnsureCanRefer(holder, Incoming());

        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanRefer(ViewerOf(UserOf(Handler), AppPermissions.HandleCorrespondence), Incoming()));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanRefer(ViewerOf(UserOf(Handler, department: Survey), AppPermissions.HandleCorrespondence, AppPermissions.ReferCorrespondence), Incoming()));
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanRefer(holder, Incoming(CorrespondenceStage.Closed)));
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanRefer(holder, Outgoing()));
    }

    [Fact]
    public void The_pending_acknowledgement_is_the_latest_unacknowledged_referral()
    {
        var c = Incoming();
        Assert.Null(DiwanRules.PendingAcknowledgement(c));

        c.Referrals.Add(new CorrespondenceReferral { Id = 1, CreatedAt = new DateTime(2026, 11, 2), AcknowledgedAt = new DateTime(2026, 11, 3) });
        Assert.Null(DiwanRules.PendingAcknowledgement(c));

        c.Referrals.Add(new CorrespondenceReferral { Id = 2, CreatedAt = new DateTime(2026, 11, 4) });
        Assert.Equal(2, DiwanRules.PendingAcknowledgement(c)!.Id);
    }

    // ───────── الحالة والإغلاق ─────────

    [Fact]
    public void A_holder_changes_the_status_within_the_branch_and_never_to_a_disabled_one()
    {
        var holder = ViewerOf(UserOf(Handler), AppPermissions.HandleCorrespondence);
        DiwanRules.EnsureCanChangeStatus(holder, Incoming(), StatusOf(CorrespondenceStage.InProgress));

        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanChangeStatus(holder, Incoming(), StatusOf(CorrespondenceStage.InProgress, OtherBranch)));
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanChangeStatus(holder, Incoming(), StatusOf(CorrespondenceStage.InProgress, active: false)));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanChangeStatus(ViewerOf(UserOf(Handler, department: Survey), AppPermissions.HandleCorrespondence), Incoming(), StatusOf(CorrespondenceStage.InProgress)));
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanChangeStatus(holder, Outgoing(), StatusOf(CorrespondenceStage.InProgress)));
    }

    [Fact]
    public void Closing_and_reopening_need_the_close_permission()
    {
        var holder = ViewerOf(UserOf(Handler), AppPermissions.HandleCorrespondence);
        var closer = ViewerOf(UserOf(Handler), AppPermissions.HandleCorrespondence, AppPermissions.CloseCorrespondence);

        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanChangeStatus(holder, Incoming(), StatusOf(CorrespondenceStage.Closed)));
        DiwanRules.EnsureCanChangeStatus(closer, Incoming(), StatusOf(CorrespondenceStage.Closed));

        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanChangeStatus(holder, Incoming(CorrespondenceStage.Closed), StatusOf(CorrespondenceStage.InProgress)));
        DiwanRules.EnsureCanChangeStatus(closer, Incoming(CorrespondenceStage.Closed), StatusOf(CorrespondenceStage.InProgress));
    }

    // ───────── التسجيل ─────────

    [Fact]
    public void Registration_is_in_the_users_own_branch_unless_organization_wide()
    {
        var clerk = ViewerOf(UserOf(Registrar), AppPermissions.RegisterCorrespondence);
        DiwanRules.EnsureCanRegister(clerk, Branch);

        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanRegister(clerk, OtherBranch));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanRegister(ViewerOf(UserOf(Registrar)), Branch));
        DiwanRules.EnsureCanRegister(ViewerOf(UserOf(Registrar, role: "SuperAdmin", branch: null, department: null)), OtherBranch);
    }

    // ───────── الصادر ─────────

    [Fact]
    public void The_drafter_edits_a_draft_and_nobody_edits_it_after_approval()
    {
        var clerk = ViewerOf(UserOf(Registrar), AppPermissions.RegisterCorrespondence);
        DiwanRules.EnsureCanEditDraft(clerk, Outgoing());

        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanEditDraft(clerk, Outgoing(OutgoingState.Approved)));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanEditDraft(ViewerOf(UserOf(Outsider)), Outgoing()));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanEditDraft(ViewerOf(UserOf(Registrar, branch: OtherBranch), AppPermissions.RegisterCorrespondence), Outgoing()));
    }

    [Fact]
    public void A_draft_is_approved_by_someone_else_with_the_text_attached()
    {
        var approver = ViewerOf(UserOf(Approver), AppPermissions.ApproveOutgoingCorrespondence);
        DiwanRules.EnsureCanDecideDraft(approver, Outgoing(), attachmentCount: 1, approve: true, note: null);

        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanDecideDraft(approver, Outgoing(), 0, true, null));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanDecideDraft(ViewerOf(UserOf(Approver)), Outgoing(), 1, true, null));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanDecideDraft(ViewerOf(UserOf(Approver, branch: OtherBranch), AppPermissions.ApproveOutgoingCorrespondence), Outgoing(), 1, true, null));
    }

    [Fact]
    public void Nobody_approves_their_own_draft_not_even_the_super_admin()
    {
        var admin = ViewerOf(UserOf(Registrar, role: "SuperAdmin", branch: null, department: null));

        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanDecideDraft(admin, Outgoing(registeredBy: Registrar), 1, true, null));
        DiwanRules.EnsureCanDecideDraft(admin, Outgoing(registeredBy: Handler), 1, true, null);
    }

    [Fact]
    public void Returning_a_draft_needs_a_reason_and_only_drafts_can_be_decided()
    {
        var approver = ViewerOf(UserOf(Approver), AppPermissions.ApproveOutgoingCorrespondence);

        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanDecideDraft(approver, Outgoing(), 0, false, "  "));
        DiwanRules.EnsureCanDecideDraft(approver, Outgoing(), 0, false, "عدّل الصياغة");
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanDecideDraft(approver, Outgoing(OutgoingState.Approved), 1, true, null));
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanDecideDraft(approver, Incoming(), 1, true, null));
    }

    [Fact]
    public void Only_an_approved_outgoing_is_issued_by_the_diwan_of_its_branch()
    {
        var clerk = ViewerOf(UserOf(Registrar), AppPermissions.RegisterCorrespondence);
        DiwanRules.EnsureCanIssue(clerk, Outgoing(OutgoingState.Approved));

        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanIssue(clerk, Outgoing(OutgoingState.Draft)));
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanIssue(clerk, Outgoing(OutgoingState.Issued)));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanIssue(ViewerOf(UserOf(Outsider)), Outgoing(OutgoingState.Approved)));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanIssue(ViewerOf(UserOf(Registrar, branch: OtherBranch), AppPermissions.RegisterCorrespondence), Outgoing(OutgoingState.Approved)));
    }

    [Fact]
    public void An_issued_outgoing_is_archived_and_locked()
    {
        var issued = Outgoing(OutgoingState.Issued);
        Assert.True(DiwanRules.IsClosed(issued));
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanAttach(ViewerOf(UserOf(Registrar), AppPermissions.RegisterCorrespondence), issued));
    }

    // ───────── المرفقات ─────────

    [Fact]
    public void Attachments_go_on_open_items_the_user_handles_or_a_draft_they_edit()
    {
        var holder = ViewerOf(UserOf(Handler), AppPermissions.HandleCorrespondence);
        DiwanRules.EnsureCanAttach(holder, Incoming());
        DiwanRules.EnsureCanAttach(ViewerOf(UserOf(Registrar), AppPermissions.RegisterCorrespondence), Outgoing());

        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanAttach(holder, Incoming(CorrespondenceStage.Closed)));
        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanAttach(ViewerOf(UserOf(Registrar), AppPermissions.RegisterCorrespondence), Outgoing(OutgoingState.Approved)));
        Assert.Throws<UnauthorizedAccessException>(() => DiwanRules.EnsureCanAttach(ViewerOf(UserOf(Handler, department: Survey), AppPermissions.HandleCorrespondence), Incoming()));
    }

    [Fact]
    public void The_registrar_may_still_attach_to_an_item_they_registered_after_referring_it()
    {
        var registrar = ViewerOf(UserOf(Registrar, department: 50), AppPermissions.RegisterCorrespondence);
        DiwanRules.EnsureCanAttach(registrar, Incoming(department: Studies));
    }

    [Fact]
    public void The_attachment_count_is_capped()
    {
        var c = Incoming();
        for (var i = 0; i < DiwanRules.MaxAttachments; i++) c.Attachments.Add(new CorrespondenceAttachment { Id = i + 1 });

        Assert.Throws<InvalidOperationException>(() => DiwanRules.EnsureCanAttach(ViewerOf(UserOf(Handler), AppPermissions.HandleCorrespondence), c));
    }
}
