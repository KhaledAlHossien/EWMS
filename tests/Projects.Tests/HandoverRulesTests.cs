using Application.Common;
using Application.Features.Handover;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>
/// قواعد الاستلام: طلب واحد غير منتهٍ لكل نوع، «بملاحظات» تشترط ملاحظة مسجَّلة، النهائي بلا ملاحظات يشترط إغلاق ما في المشروع،
/// من عالج الملاحظة لا يتحقق منها، وإغلاق المشروع تنبيه لا منع.
/// </summary>
public class HandoverRulesTests
{
    private const int Branch = 1;
    private static readonly DateTime Today = new(2026, 10, 31);

    private static User UserOf(int id, string role = "Emp", int? branch = Branch, bool active = true) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1, IsActive = active,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = 10
    };

    private static Viewer ViewerOf(User user, params string[] permissions) =>
        new(user, new HashSet<string>(permissions.Append(AppPermissions.ViewBranchProjects)));

    private static Project ProjectOf(ProjectStage stage = ProjectStage.Execution) => new() { Id = 1, Name = "p", BranchId = Branch, Stage = stage, CreatedByUserId = 99 };

    private static HandoverRecord RecordOf(Project project, HandoverKind kind = HandoverKind.Initial, HandoverStatus status = HandoverStatus.Requested,
        HandoverResult? result = null, params HandoverNote[] notes) => new()
    {
        Id = 9, ProjectId = project.Id, Project = project, Kind = kind, Status = status, Result = result,
        RequestedAt = Today.AddDays(-10), Notes = notes.ToList()
    };

    private static HandoverNote NoteOf(int id, HandoverNoteStatus status = HandoverNoteStatus.Open, int? fixedBy = null, DateTime? due = null) =>
        new() { Id = id, Description = "ملاحظة " + id, Status = status, FixedByUserId = fixedBy, DueDate = due };

    private static readonly Viewer Request = ViewerOf(UserOf(2), AppPermissions.RequestHandover);
    private static readonly Viewer Manage = ViewerOf(UserOf(3), AppPermissions.ManageHandover);
    private static Viewer Follow(int id = 4) => ViewerOf(UserOf(id), AppPermissions.FollowHandoverNotes);

    // ───────── الأسماء والرؤية ─────────

    [Fact]
    public void The_internal_number_follows_the_creation_year_and_pads_the_id()
    {
        Assert.Equal("HND-2026-00007", HandoverRules.Number(7, new DateTime(2026, 10, 31)));
    }

    [Fact]
    public void Someone_without_any_handover_permission_sees_nothing_and_the_project_boundary_applies()
    {
        var project = ProjectOf();
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanView(ViewerOf(UserOf(2)), project));
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanView(ViewerOf(UserOf(3, branch: 2), AppPermissions.ViewHandovers), project));
        HandoverRules.EnsureCanView(ViewerOf(UserOf(4), AppPermissions.ViewHandovers), project);
        HandoverRules.EnsureCanView(ViewerOf(UserOf(5), AppPermissions.FollowHandoverNotes), project);
    }

    // ───────── الطلب ─────────

    [Fact]
    public void A_request_needs_the_permission_an_executing_project_and_no_unfinished_request_of_the_same_kind()
    {
        var project = ProjectOf();
        HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, null, [], "ملاحظة");
        HandoverRules.EnsureCanRequest(Request, ProjectOf(ProjectStage.Handover), HandoverKind.Final, null, [], null);

        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanRequest(ViewerOf(UserOf(2), AppPermissions.ViewHandovers), project, HandoverKind.Initial, null, [], null));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, ProjectOf(ProjectStage.Study), HandoverKind.Initial, null, [], null));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, ProjectOf(ProjectStage.Contracting), HandoverKind.Initial, null, [], null));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, ProjectOf(ProjectStage.Closed), HandoverKind.Initial, null, [], null));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, project, (HandoverKind)7, null, [], null));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, null, [], new string('أ', 1001)));

        // أولي بانتظار لجنة: لا طلب أولي ثانٍ، لكن النهائي مستقل
        var pending = new[] { RecordOf(project, HandoverKind.Initial, HandoverStatus.Requested) };
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, null, pending, null));
        HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Final, null, pending, null);

        var formed = new[] { RecordOf(project, HandoverKind.Initial, HandoverStatus.CommitteeFormed) };
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, null, formed, null));

        // أولي منتهٍ بملاحظات أو ملغى: طلب جديد مسموح (إعادة الطلب)
        HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, null, [RecordOf(project, HandoverKind.Initial, HandoverStatus.Concluded, HandoverResult.WithNotes)], null);
        HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, null, [RecordOf(project, HandoverKind.Initial, HandoverStatus.Cancelled)], null);
    }

    [Fact]
    public void A_final_handover_without_notes_is_recorded_once_per_project()
    {
        var project = ProjectOf(ProjectStage.Handover);
        var done = RecordOf(project, HandoverKind.Final, HandoverStatus.Concluded, HandoverResult.WithoutNotes);
        Assert.True(HandoverRules.IsFinalWithoutNotes(done));
        Assert.False(HandoverRules.IsFinalWithoutNotes(RecordOf(project, HandoverKind.Final, HandoverStatus.Concluded, HandoverResult.WithNotes)));
        Assert.False(HandoverRules.IsFinalWithoutNotes(RecordOf(project, HandoverKind.Initial, HandoverStatus.Concluded, HandoverResult.WithoutNotes)));

        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Final, null, [done], null));
        // لا يمنع ذلك طلباً أولياً (سجل مختلف)
        HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, null, [done], null);
    }

    [Fact]
    public void A_request_may_name_a_contract_of_the_same_project_that_is_active_or_completed()
    {
        var project = ProjectOf();
        Contract ContractOf(int projectId, ContractStatus status) => new() { Id = 5, ProjectId = projectId, Status = status };

        HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, ContractOf(1, ContractStatus.Active), [], null);
        HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, ContractOf(1, ContractStatus.Completed), [], null);
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, ContractOf(2, ContractStatus.Active), [], null));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, ContractOf(1, ContractStatus.Contracting), [], null));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRequest(Request, project, HandoverKind.Initial, ContractOf(1, ContractStatus.Terminated), [], null));
    }

    // ───────── اللجنة والكشف ─────────

    [Fact]
    public void The_committee_is_formed_once_for_a_pending_request()
    {
        var project = ProjectOf();
        HandoverRules.EnsureCanFormCommittee(Manage, RecordOf(project));
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanFormCommittee(Request, RecordOf(project)));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanFormCommittee(Manage, RecordOf(project, status: HandoverStatus.CommitteeFormed)));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanFormCommittee(Manage, RecordOf(project, status: HandoverStatus.Cancelled)));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanFormCommittee(Manage, RecordOf(ProjectOf(ProjectStage.Closed))));
        var withCommittee = RecordOf(project);
        withCommittee.Committee = new Committee();
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanFormCommittee(Manage, withCommittee));
    }

    [Fact]
    public void The_committee_is_edited_only_while_formed_and_before_the_result()
    {
        var project = ProjectOf();
        HandoverRules.EnsureCanEditCommittee(Manage, RecordOf(project, status: HandoverStatus.CommitteeFormed));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanEditCommittee(Manage, RecordOf(project)));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanEditCommittee(Manage, RecordOf(project, status: HandoverStatus.Concluded, result: HandoverResult.WithNotes)));
    }

    [Fact]
    public void The_inspection_date_is_neither_before_the_request_nor_in_the_future()
    {
        var record = RecordOf(ProjectOf());
        Assert.Null(HandoverRules.ValidateInspection(record, Today, Today));
        Assert.Null(HandoverRules.ValidateInspection(record, record.RequestedAt, Today));
        Assert.NotNull(HandoverRules.ValidateInspection(record, record.RequestedAt.AddDays(-1), Today));
        Assert.NotNull(HandoverRules.ValidateInspection(record, Today.AddDays(1), Today));
    }

    // ───────── النتيجة ─────────

    private static void Conclude(HandoverRecord record, HandoverResult result, int otherOpen = 0, DateTime? inspection = null, Viewer? viewer = null) =>
        HandoverRules.EnsureCanConclude(viewer ?? Manage, record, result, inspection ?? Today, otherOpen, null, Today);

    [Fact]
    public void Concluding_needs_the_committee_and_a_valid_result_by_the_manager()
    {
        var project = ProjectOf();
        Conclude(RecordOf(project, status: HandoverStatus.CommitteeFormed), HandoverResult.WithoutNotes);

        Assert.Throws<InvalidOperationException>(() => Conclude(RecordOf(project), HandoverResult.WithoutNotes));                                          // لجنة لم تُشكَّل
        Assert.Throws<InvalidOperationException>(() => Conclude(RecordOf(project, status: HandoverStatus.Concluded, result: HandoverResult.WithNotes), HandoverResult.WithoutNotes));
        Assert.Throws<InvalidOperationException>(() => Conclude(RecordOf(project, status: HandoverStatus.CommitteeFormed), (HandoverResult)8));
        Assert.Throws<InvalidOperationException>(() => Conclude(RecordOf(project, status: HandoverStatus.CommitteeFormed), HandoverResult.WithoutNotes, inspection: Today.AddDays(1)));
        Assert.Throws<UnauthorizedAccessException>(() => Conclude(RecordOf(project, status: HandoverStatus.CommitteeFormed), HandoverResult.WithoutNotes, viewer: Request));
    }

    [Fact]
    public void A_with_notes_result_needs_at_least_one_recorded_note_and_without_notes_forbids_them()
    {
        var project = ProjectOf();
        Assert.Throws<InvalidOperationException>(() => Conclude(RecordOf(project, status: HandoverStatus.CommitteeFormed), HandoverResult.WithNotes));
        Conclude(RecordOf(project, status: HandoverStatus.CommitteeFormed, notes: NoteOf(1)), HandoverResult.WithNotes);
        Assert.Throws<InvalidOperationException>(() => Conclude(RecordOf(project, status: HandoverStatus.CommitteeFormed, notes: NoteOf(1)), HandoverResult.WithoutNotes));
    }

    [Fact]
    public void A_final_without_notes_waits_for_every_open_note_in_the_project_to_close_but_an_initial_does_not()
    {
        var project = ProjectOf(ProjectStage.Handover);
        Conclude(RecordOf(project, HandoverKind.Final, HandoverStatus.CommitteeFormed), HandoverResult.WithoutNotes);
        Assert.Throws<InvalidOperationException>(() => Conclude(RecordOf(project, HandoverKind.Final, HandoverStatus.CommitteeFormed), HandoverResult.WithoutNotes, otherOpen: 2));
        Conclude(RecordOf(project, HandoverKind.Initial, HandoverStatus.CommitteeFormed), HandoverResult.WithoutNotes, otherOpen: 2);
        // «بملاحظات» لا تتأثر بما في السجلات الأخرى
        Conclude(RecordOf(project, HandoverKind.Final, HandoverStatus.CommitteeFormed, notes: NoteOf(1)), HandoverResult.WithNotes, otherOpen: 2);
    }

    [Fact]
    public void A_request_is_cancelled_instead_of_deleted_until_it_concludes()
    {
        var project = ProjectOf();
        HandoverRules.EnsureCanCancel(Manage, RecordOf(project));
        HandoverRules.EnsureCanCancel(Manage, RecordOf(project, status: HandoverStatus.CommitteeFormed));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanCancel(Manage, RecordOf(project, status: HandoverStatus.Concluded, result: HandoverResult.WithoutNotes)));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanCancel(Manage, RecordOf(project, status: HandoverStatus.Cancelled)));
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanCancel(Request, RecordOf(project)));
    }

    // ───────── ملاحظات اللجنة ─────────

    [Fact]
    public void A_note_needs_a_description_and_one_responsible_at_most()
    {
        Assert.Null(HandoverRules.ValidateNote("تشققات في الجدار الشمالي", null, null, null, Today));
        Assert.Null(HandoverRules.ValidateNote("ملاحظة", 5, null, Today.AddDays(30), Today));
        Assert.Null(HandoverRules.ValidateNote("ملاحظة", null, "المقاول", null, Today));
        Assert.NotNull(HandoverRules.ValidateNote("  ", null, null, null, Today));
        Assert.NotNull(HandoverRules.ValidateNote(new string('أ', 2001), null, null, null, Today));
        Assert.NotNull(HandoverRules.ValidateNote("ملاحظة", 5, "المقاول", null, Today));                       // الاثنان معاً
        Assert.NotNull(HandoverRules.ValidateNote("ملاحظة", null, new string('م', 151), null, Today));
        Assert.NotNull(HandoverRules.ValidateNote("ملاحظة", null, null, Today.AddYears(-2), Today));
    }

    [Fact]
    public void The_responsible_employee_is_active_and_from_the_project_branch()
    {
        var project = ProjectOf();
        Assert.Null(HandoverRules.ValidateResponsible(project, null));
        Assert.Null(HandoverRules.ValidateResponsible(project, UserOf(5)));
        Assert.NotNull(HandoverRules.ValidateResponsible(project, UserOf(5, active: false)));
        Assert.NotNull(HandoverRules.ValidateResponsible(project, UserOf(5, branch: 2)));
    }

    [Fact]
    public void Notes_are_added_during_the_inspection_or_after_a_with_notes_result_never_before_the_committee_or_after_a_clean_result()
    {
        var project = ProjectOf();
        HandoverRules.EnsureCanAddNote(Follow(), RecordOf(project, status: HandoverStatus.CommitteeFormed));
        HandoverRules.EnsureCanAddNote(Follow(), RecordOf(project, status: HandoverStatus.Concluded, result: HandoverResult.WithNotes));

        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanAddNote(Follow(), RecordOf(project)));                                      // قبل اللجنة
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanAddNote(Follow(), RecordOf(project, status: HandoverStatus.Concluded, result: HandoverResult.WithoutNotes)));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanAddNote(Follow(), RecordOf(project, status: HandoverStatus.Cancelled)));
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanAddNote(Manage, RecordOf(project, status: HandoverStatus.CommitteeFormed)));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanAddNote(Follow(), RecordOf(project, status: HandoverStatus.CommitteeFormed,
            notes: Enumerable.Range(1, HandoverRules.MaxNotesPerRecord).Select(i => NoteOf(i)).ToArray())));
    }

    [Fact]
    public void An_open_note_is_edited_and_a_concluded_record_keeps_its_notes()
    {
        var project = ProjectOf();
        var formed = RecordOf(project, status: HandoverStatus.CommitteeFormed);
        var concluded = RecordOf(project, status: HandoverStatus.Concluded, result: HandoverResult.WithNotes);

        HandoverRules.EnsureCanEditNote(Follow(), formed, NoteOf(1));
        HandoverRules.EnsureCanEditNote(Follow(), concluded, NoteOf(1));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanEditNote(Follow(), formed, NoteOf(1, HandoverNoteStatus.Fixed)));

        HandoverRules.EnsureCanRemoveNote(Follow(), formed, NoteOf(1));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRemoveNote(Follow(), concluded, NoteOf(1)));
    }

    [Fact]
    public void Fixing_a_note_needs_the_fix_text_and_an_open_note_in_a_record_with_a_committee()
    {
        var project = ProjectOf();
        var concluded = RecordOf(project, status: HandoverStatus.Concluded, result: HandoverResult.WithNotes);

        HandoverRules.EnsureCanMarkFixed(Follow(), concluded, NoteOf(1), "رُممت التشققات");
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanMarkFixed(Follow(), concluded, NoteOf(1), " "));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanMarkFixed(Follow(), concluded, NoteOf(1, HandoverNoteStatus.Fixed), "س"));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanMarkFixed(Follow(), concluded, NoteOf(1, HandoverNoteStatus.Verified), "س"));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanMarkFixed(Follow(), RecordOf(project), NoteOf(1), "س"));
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanMarkFixed(Manage, concluded, NoteOf(1), "س"));
    }

    [Fact]
    public void Whoever_fixed_the_note_does_not_verify_it_not_even_the_system_admin()
    {
        var project = ProjectOf();
        var concluded = RecordOf(project, status: HandoverStatus.Concluded, result: HandoverResult.WithNotes);
        var fixedByFour = NoteOf(1, HandoverNoteStatus.Fixed, fixedBy: 4);

        HandoverRules.EnsureCanVerify(Follow(8), concluded, fixedByFour);
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanVerify(Follow(4), concluded, fixedByFour));
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanVerify(ViewerOf(UserOf(4, "SuperAdmin"), AppPermissions.FollowHandoverNotes), concluded, fixedByFour));
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanVerify(Follow(8), concluded, NoteOf(1)));                                  // لم تُعالَج
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanVerify(Follow(8), concluded, NoteOf(1, HandoverNoteStatus.Verified, fixedBy: 4)));
    }

    [Fact]
    public void Rejecting_a_fix_needs_a_reason_and_comes_from_a_different_person_than_the_fixer()
    {
        var concluded = RecordOf(ProjectOf(), status: HandoverStatus.Concluded, result: HandoverResult.WithNotes);
        var fixedByFour = NoteOf(1, HandoverNoteStatus.Fixed, fixedBy: 4);

        HandoverRules.EnsureCanRejectFix(Follow(8), concluded, fixedByFour, "لم يُعالَج كما ينبغي");
        Assert.Throws<InvalidOperationException>(() => HandoverRules.EnsureCanRejectFix(Follow(8), concluded, fixedByFour, " "));
        Assert.Throws<UnauthorizedAccessException>(() => HandoverRules.EnsureCanRejectFix(Follow(4), concluded, fixedByFour, "سبب"));
    }

    // ───────── المتابعة والتنبيه لا القرار ─────────

    [Fact]
    public void Open_notes_count_everything_not_yet_verified_and_an_overdue_note_is_only_a_candidate_for_attention()
    {
        var notes = new[] { NoteOf(1), NoteOf(2, HandoverNoteStatus.Fixed), NoteOf(3, HandoverNoteStatus.Verified) };
        Assert.Equal(2, HandoverRules.OpenNotes(notes));

        Assert.True(HandoverRules.IsNoteOverdue(NoteOf(1, due: Today.AddDays(-1)), Today));
        Assert.False(HandoverRules.IsNoteOverdue(NoteOf(1, due: Today), Today));
        Assert.False(HandoverRules.IsNoteOverdue(NoteOf(1), Today));                                                         // بلا موعد
        Assert.False(HandoverRules.IsNoteOverdue(NoteOf(1, HandoverNoteStatus.Verified, due: Today.AddDays(-9)), Today));    // مغلقة
    }

    [Fact]
    public void Closure_warnings_list_what_is_missing_but_never_block_closing()
    {
        var project = ProjectOf(ProjectStage.Handover);
        var clean = RecordOf(project, HandoverKind.Final, HandoverStatus.Concluded, HandoverResult.WithoutNotes);
        Assert.Empty(HandoverRules.ClosureWarnings([clean], [new Contract { Status = ContractStatus.Completed }]));

        var warnings = HandoverRules.ClosureWarnings(
            [RecordOf(project, HandoverKind.Initial, HandoverStatus.Concluded, HandoverResult.WithNotes, NoteOf(1), NoteOf(2, HandoverNoteStatus.Verified)),
             RecordOf(project, HandoverKind.Final, HandoverStatus.Requested)],
            [new Contract
            {
                Status = ContractStatus.Active,
                RetentionEntries = [new RetentionEntry { Amount = 500m }]
            }]);

        Assert.Contains(warnings, w => w.Contains("استلام نهائي"));
        Assert.Contains(warnings, w => w.Contains("1 ملاحظة"));
        Assert.Contains(warnings, w => w.Contains("طلب استلام لم ينتهِ"));
        Assert.Contains(warnings, w => w.Contains("محتجزة"));
        Assert.Contains(warnings, w => w.Contains("سارياً"));

        // العقد الملغى لا يُحتسب، والمبلغ المفرج عنه لا يُنبَّه عليه
        var released = new RetentionEntry { Amount = 500m, Decisions = [new RetentionDecision { Decision = RetentionDecisionKind.Release, DecidedAt = Today }] };
        Assert.Empty(HandoverRules.ClosureWarnings([clean], [new Contract { Status = ContractStatus.Cancelled, RetentionEntries = [new RetentionEntry { Amount = 9m }] },
            new Contract { Status = ContractStatus.Completed, RetentionEntries = [released] }]));
    }
}
