using Application.Common;
using Application.Features.Contracts;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>
/// قواعد العقود والمقاولين: حدّها حدّ المشروع، تجمّد العقد بعد سريانه، الإرساء على عرض واحد، الملاحق والمحتجزات سجلات تُضاف،
/// الإفراج نهائي، تنبيه الضمان لا يقرر، والمراجع غير المُعدّ حتى لمدير النظام. لا معادلة للاحتجاز ولا تصنيف للمقاول.
/// </summary>
public class ContractRulesTests
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

    private static Project ProjectOf(ProjectStage stage = ProjectStage.Contracting, params User[] members) => new()
    {
        Id = 1, Name = "p", BranchId = Branch, Stage = stage, CreatedByUserId = 99,
        Members = members.Select(m => new ProjectMember { ProjectId = 1, UserId = m.Id, User = m }).ToList()
    };

    private static Contract ContractOf(Project project, ContractStatus status = ContractStatus.Contracting, ContractingMethod method = ContractingMethod.Direct) => new()
    {
        Id = 5, ProjectId = project.Id, Project = project, BranchId = Branch, Status = status, Method = method,
        CreatedByUser = UserOf(99), Number = "ع/12", ContractorId = 3, ContractDate = Today.AddDays(-5), Value = 1_000_000m, DurationDays = 200
    };

    private static ContractOffer OfferOf(int id, int contractorId, bool awarded = false) => new()
    {
        Id = id, ContractorId = contractorId, Amount = 900_000m, IsAwarded = awarded,
        Contractor = new Contractor { Id = contractorId, BranchId = Branch, Name = "م" + contractorId }
    };

    // ───────── الأسماء والرؤية ─────────

    [Fact]
    public void The_internal_number_follows_the_creation_year_and_pads_the_id()
    {
        Assert.Equal("CON-2026-00042", ContractRules.Number(42, new DateTime(2026, 3, 1)));
    }

    [Fact]
    public void Someone_without_any_contract_permission_sees_nothing_and_the_project_boundary_still_applies()
    {
        var project = ProjectOf();
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanView(ViewerOf(UserOf(2)), project));

        // يملك الصلاحية لكن المشروع من فرع آخر
        var elsewhere = ViewerOf(UserOf(3, branch: 2), AppPermissions.ViewContracts);
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanView(elsewhere, project));

        ContractRules.EnsureCanView(ViewerOf(UserOf(4), AppPermissions.ViewContracts), project);   // لا يرمي: رؤية المشروع نفسها (فرعه)
    }

    [Fact]
    public void Contractors_and_criteria_belong_to_the_branch_unless_the_viewer_sees_the_whole_organization()
    {
        ContractRules.EnsureSameBranch(ViewerOf(UserOf(2)), Branch);
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureSameBranch(ViewerOf(UserOf(2)), 2));
        ContractRules.EnsureSameBranch(ViewerOf(UserOf(2), AppPermissions.ViewOrganizationDashboard), 2);
    }

    // ───────── المقاول والمعيار ─────────

    [Fact]
    public void A_contractor_needs_a_name_and_the_branch_one_must_be_active_to_be_picked()
    {
        Assert.NotNull(ContractRules.ValidateContractor("  ", null, null, null, null, null));
        Assert.NotNull(ContractRules.ValidateContractor(new string('م', 201), null, null, null, null, null));
        Assert.Null(ContractRules.ValidateContractor("شركة البناء", "123", "أحمد", "0999", "دمشق", "ملاحظة"));

        ContractRules.EnsureContractorUsable(new Contractor { BranchId = Branch, IsActive = true }, Branch);
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureContractorUsable(new Contractor { BranchId = 2, IsActive = true }, Branch));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureContractorUsable(new Contractor { BranchId = Branch, IsActive = false }, Branch));
    }

    [Fact]
    public void A_criterion_weight_is_optional_but_positive_and_at_most_100()
    {
        Assert.Null(ContractRules.ValidateCriterion("الجودة", null, null, 0));
        Assert.Null(ContractRules.ValidateCriterion("الجودة", "وصف", 30m, 10));
        Assert.NotNull(ContractRules.ValidateCriterion("", null, null, 0));
        Assert.NotNull(ContractRules.ValidateCriterion("الجودة", null, 0m, 0));
        Assert.NotNull(ContractRules.ValidateCriterion("الجودة", null, 101m, 0));
        Assert.NotNull(ContractRules.ValidateCriterion("الجودة", null, null, 1001));
    }

    // ───────── بيانات العقد ─────────

    private static string? Validate(ContractingMethod method = ContractingMethod.Direct, string? number = "ع/1", decimal? value = 100m, string? currency = "SYP", int? days = 100,
        DateTime? contractDate = null, DateTime? start = null, string? tender = null, DateTime? tenderSent = null, DateTime? opened = null, DateTime? awarded = null,
        int? months = null, DateTime? wStart = null, DateTime? wEnd = null) =>
        ContractRules.ValidateContract(method, number, value, currency, days, contractDate, start, null, null, tender, tenderSent, opened, awarded, null, months, wStart, wEnd, Today);

    [Fact]
    public void Contract_data_is_checked_without_inventing_any_formula()
    {
        Assert.Null(Validate());
        Assert.Null(Validate(value: null, days: null, currency: "USD"));
        Assert.NotNull(Validate(value: -1m));
        Assert.NotNull(Validate(currency: "sy"));
        Assert.NotNull(Validate(currency: "SYPP"));
        Assert.NotNull(Validate(days: 0));
        Assert.NotNull(Validate(days: ContractRules.MaxDurationDays + 1));
        Assert.NotNull(Validate(number: new string('1', 101)));
        Assert.NotNull(Validate(contractDate: Today.AddYears(2)));
        Assert.NotNull(Validate(contractDate: Today, start: Today.AddDays(-1)));      // البدء قبل العقد
        Assert.Null(Validate(contractDate: Today, start: Today));
    }

    [Fact]
    public void Tender_data_belongs_to_the_tender_method_only_and_its_dates_follow_the_procedure_order()
    {
        Assert.NotNull(Validate(ContractingMethod.Direct, tender: "م/1"));
        Assert.NotNull(Validate(ContractingMethod.PriceQuotes, tenderSent: Today));
        Assert.Null(Validate(ContractingMethod.Tender, tender: "م/1", tenderSent: Today.AddDays(-20), opened: Today.AddDays(-10), awarded: Today.AddDays(-5)));
        Assert.NotNull(Validate(ContractingMethod.Tender, tenderSent: Today, opened: Today.AddDays(-1)));       // الفض قبل الإرسال
        Assert.NotNull(Validate(ContractingMethod.Tender, opened: Today, awarded: Today.AddDays(-1)));          // الإرساء قبل الفض
    }

    [Fact]
    public void Warranty_dates_must_be_ordered_and_the_end_is_only_suggested_never_filled_automatically()
    {
        Assert.Null(Validate(months: 12, wStart: Today, wEnd: Today.AddYears(1)));
        Assert.NotNull(Validate(months: 0));
        Assert.NotNull(Validate(months: ContractRules.MaxWarrantyMonths + 1));
        Assert.NotNull(Validate(wStart: Today, wEnd: Today.AddDays(-1)));

        Assert.Equal(new DateTime(2027, 10, 30), ContractRules.SuggestedWarrantyEnd(new DateTime(2026, 10, 31), 12));
        Assert.Null(ContractRules.SuggestedWarrantyEnd(null, 12));
        Assert.Null(ContractRules.SuggestedWarrantyEnd(Today, null));
    }

    // ───────── الإنشاء والتعديل والتجمّد ─────────

    [Fact]
    public void Creating_a_contract_needs_the_permission_a_visible_open_project()
    {
        var project = ProjectOf();
        ContractRules.EnsureCanCreate(ViewerOf(UserOf(2), AppPermissions.CreateContract), project);
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanCreate(ViewerOf(UserOf(2), AppPermissions.ViewContracts), project));
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanCreate(ViewerOf(UserOf(3, branch: 2), AppPermissions.CreateContract), project));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanCreate(ViewerOf(UserOf(2), AppPermissions.CreateContract), ProjectOf(ProjectStage.Closed)));
    }

    [Fact]
    public void A_terminated_or_cancelled_contract_is_not_edited_and_a_finished_project_blocks_edits()
    {
        var edit = ViewerOf(UserOf(2), AppPermissions.EditContract);
        ContractRules.EnsureCanEdit(edit, ContractOf(ProjectOf()));
        ContractRules.EnsureCanEdit(edit, ContractOf(ProjectOf(), ContractStatus.Active));
        ContractRules.EnsureCanEdit(edit, ContractOf(ProjectOf(), ContractStatus.Completed));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanEdit(edit, ContractOf(ProjectOf(), ContractStatus.Terminated)));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanEdit(edit, ContractOf(ProjectOf(), ContractStatus.Cancelled)));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanEdit(edit, ContractOf(ProjectOf(ProjectStage.Cancelled))));
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanEdit(ViewerOf(UserOf(2), AppPermissions.ViewContracts), ContractOf(ProjectOf())));
    }

    [Fact]
    public void After_activation_the_method_contractor_value_and_duration_are_frozen_because_amendments_change_them()
    {
        var draft = ContractOf(ProjectOf());
        ContractRules.EnsureCoreFieldsUnchanged(draft, ContractingMethod.Tender, 9, 5m, 10);     // قيد التعاقد: حرّ

        var active = ContractOf(ProjectOf(), ContractStatus.Active);
        ContractRules.EnsureCoreFieldsUnchanged(active, active.Method, active.ContractorId, active.Value, active.DurationDays);
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCoreFieldsUnchanged(active, ContractingMethod.Tender, active.ContractorId, active.Value, active.DurationDays));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCoreFieldsUnchanged(active, active.Method, 9, active.Value, active.DurationDays));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCoreFieldsUnchanged(active, active.Method, active.ContractorId, 2m, active.DurationDays));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCoreFieldsUnchanged(active, active.Method, active.ContractorId, active.Value, 7));
    }

    // ───────── الحالة والسريان ─────────

    [Theory]
    [InlineData(ContractStatus.Contracting, ContractStatus.Active, true)]
    [InlineData(ContractStatus.Contracting, ContractStatus.Cancelled, true)]
    [InlineData(ContractStatus.Active, ContractStatus.Completed, true)]
    [InlineData(ContractStatus.Active, ContractStatus.Terminated, true)]
    [InlineData(ContractStatus.Contracting, ContractStatus.Completed, false)]
    [InlineData(ContractStatus.Active, ContractStatus.Cancelled, false)]
    [InlineData(ContractStatus.Completed, ContractStatus.Active, false)]
    [InlineData(ContractStatus.Terminated, ContractStatus.Active, false)]
    [InlineData(ContractStatus.Cancelled, ContractStatus.Contracting, false)]
    public void Only_the_manual_transitions_in_the_flow_are_allowed(ContractStatus from, ContractStatus to, bool allowed)
    {
        Assert.Equal(allowed, ContractRules.CanTransition(from, to));
    }

    [Fact]
    public void Activation_needs_the_official_number_the_contractor_the_date_and_the_value()
    {
        var edit = ViewerOf(UserOf(2), AppPermissions.EditContract);
        var ok = ContractOf(ProjectOf());
        ContractRules.EnsureCanChangeStatus(edit, ok, ContractStatus.Active, null);

        foreach (var broken in new Action<Contract>[] { c => c.Number = " ", c => c.ContractorId = null, c => c.ContractDate = null, c => c.Value = null })
        {
            var c = ContractOf(ProjectOf());
            broken(c);
            Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanChangeStatus(edit, c, ContractStatus.Active, null));
        }
    }

    [Fact]
    public void A_tender_or_quotes_contract_activates_only_for_the_contractor_whose_offer_was_awarded()
    {
        var edit = ViewerOf(UserOf(2), AppPermissions.EditContract);
        var tender = ContractOf(ProjectOf(), method: ContractingMethod.Tender);
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanChangeStatus(edit, tender, ContractStatus.Active, null));          // لا عرض مُرسًّى عليه

        tender.Offers.Add(OfferOf(1, 3, awarded: true));
        ContractRules.EnsureCanChangeStatus(edit, tender, ContractStatus.Active, null);

        tender.ContractorId = 4;                                                                                                                  // متعاقد غير صاحب العرض
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanChangeStatus(edit, tender, ContractStatus.Active, null));
    }

    [Fact]
    public void Terminating_or_cancelling_needs_a_reason_and_a_finished_contract_cannot_move_again()
    {
        var edit = ViewerOf(UserOf(2), AppPermissions.EditContract);
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanChangeStatus(edit, ContractOf(ProjectOf(), ContractStatus.Active), ContractStatus.Terminated, " "));
        ContractRules.EnsureCanChangeStatus(edit, ContractOf(ProjectOf(), ContractStatus.Active), ContractStatus.Terminated, "تقصير");
        ContractRules.EnsureCanChangeStatus(edit, ContractOf(ProjectOf(), ContractStatus.Active), ContractStatus.Completed, null);
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanChangeStatus(edit, ContractOf(ProjectOf(), ContractStatus.Completed), ContractStatus.Active, null));
    }

    // ───────── العروض والإرساء ─────────

    [Fact]
    public void Offers_are_for_tenders_and_quotes_before_the_award_one_per_active_contractor_of_the_branch()
    {
        var edit = ViewerOf(UserOf(2), AppPermissions.EditContract);
        var contractor = new Contractor { Id = 7, BranchId = Branch, IsActive = true };

        var tender = ContractOf(ProjectOf(), method: ContractingMethod.Tender);
        ContractRules.EnsureCanAddOffer(edit, tender, contractor, 500m);
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAddOffer(edit, tender, contractor, -1m));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAddOffer(edit, ContractOf(ProjectOf()), contractor, 1m));                   // مباشر
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAddOffer(edit, ContractOf(ProjectOf(), ContractStatus.Active, ContractingMethod.Tender), contractor, 1m));

        tender.Offers.Add(OfferOf(1, 7));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAddOffer(edit, tender, contractor, 1m));                                    // عرض مكرر
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAddOffer(edit, tender, new Contractor { Id = 8, BranchId = 2, IsActive = true }, 1m));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAddOffer(edit, tender, new Contractor { Id = 9, BranchId = Branch, IsActive = false }, 1m));

        tender.Offers.Add(OfferOf(2, 8, awarded: true));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAddOffer(edit, tender, new Contractor { Id = 10, BranchId = Branch, IsActive = true }, 1m));   // رسا التعاقد
    }

    [Fact]
    public void Only_one_offer_is_awarded_and_only_before_the_contract_is_active()
    {
        var edit = ViewerOf(UserOf(2), AppPermissions.EditContract);
        var tender = ContractOf(ProjectOf(), method: ContractingMethod.PriceQuotes);
        var a = OfferOf(1, 3);
        var b = OfferOf(2, 4);
        tender.Offers.Add(a);
        tender.Offers.Add(b);

        ContractRules.EnsureCanAward(edit, tender, a);
        a.IsAwarded = true;
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAward(edit, tender, b));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAward(edit, ContractOf(ProjectOf()), a));                        // مباشر
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanAward(edit, ContractOf(ProjectOf(), ContractStatus.Active, ContractingMethod.Tender), b));
    }

    // ───────── الملاحق ─────────

    private static void AddAmendment(Contract contract, decimal? value = null, int? days = null, string? title = "ملحق", string? description = null, DateTime? approved = null, Viewer? viewer = null) =>
        ContractRules.EnsureCanAddAmendment(viewer ?? ViewerOf(UserOf(2), AppPermissions.AddContractAmendment), contract, title, description, value, days, approved ?? Today, Today);

    [Fact]
    public void An_amendment_is_added_to_an_active_or_completed_contract_with_a_title_a_date_and_some_effect()
    {
        AddAmendment(ContractOf(ProjectOf(), ContractStatus.Active), value: 50_000m);
        AddAmendment(ContractOf(ProjectOf(), ContractStatus.Completed), days: 30);
        AddAmendment(ContractOf(ProjectOf(), ContractStatus.Active), description: "تعديل نطاق الأعمال");

        Assert.Throws<InvalidOperationException>(() => AddAmendment(ContractOf(ProjectOf()), value: 1m));                                          // قيد التعاقد
        Assert.Throws<InvalidOperationException>(() => AddAmendment(ContractOf(ProjectOf(), ContractStatus.Terminated), value: 1m));
        Assert.Throws<InvalidOperationException>(() => AddAmendment(ContractOf(ProjectOf(), ContractStatus.Active), value: 1m, title: " "));
        Assert.Throws<InvalidOperationException>(() => AddAmendment(ContractOf(ProjectOf(), ContractStatus.Active), value: 1m, approved: Today.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() => AddAmendment(ContractOf(ProjectOf(), ContractStatus.Active)));                              // بلا أثر ولا شرح
        Assert.Throws<UnauthorizedAccessException>(() => AddAmendment(ContractOf(ProjectOf(), ContractStatus.Active), value: 1m, viewer: ViewerOf(UserOf(2), AppPermissions.EditContract)));
    }

    [Fact]
    public void Amendments_adjust_the_effective_value_and_duration_by_simple_sums_and_cannot_push_them_to_zero()
    {
        var contract = ContractOf(ProjectOf(), ContractStatus.Active);
        Assert.Equal(1, ContractRules.NextAmendmentNumber(contract));
        contract.Amendments.Add(new ContractAmendment { Number = 1, ValueChange = 200_000m, DurationChangeDays = 30 });
        contract.Amendments.Add(new ContractAmendment { Number = 2, ValueChange = -50_000m });

        Assert.Equal(3, ContractRules.NextAmendmentNumber(contract));
        Assert.Equal(1_150_000m, ContractRules.EffectiveValue(contract));
        Assert.Equal(230, ContractRules.EffectiveDurationDays(contract));

        Assert.Throws<InvalidOperationException>(() => AddAmendment(contract, value: -1_200_000m));
        Assert.Throws<InvalidOperationException>(() => AddAmendment(contract, days: -230));
        AddAmendment(contract, value: -1_150_000m);   // يبلغ الصفر بالضبط: مقبول

        Assert.Null(ContractRules.EffectiveValue(new Contract { Value = null }));
        Assert.Null(ContractRules.EffectiveDurationDays(new Contract { DurationDays = null }));
    }

    // ───────── اللجان ─────────

    private static ContractRules.MemberInput Member(int? user, string? name = null, string? capacity = "عضو", bool chair = false) => new(user, name, capacity, chair);

    [Fact]
    public void A_committee_has_one_chair_each_member_is_an_employee_or_an_outsider_with_a_capacity_and_no_duplicates()
    {
        Assert.Null(ContractRules.ValidateMembers([Member(1, chair: true, capacity: "رئيس قسم الإشراف"), Member(2, capacity: "مسؤول العقود"), Member(null, "ممثل خارجي", "ممثل المساحة")]));

        Assert.NotNull(ContractRules.ValidateMembers([]));
        Assert.NotNull(ContractRules.ValidateMembers([Member(1), Member(2)]));                                      // بلا رئيس
        Assert.NotNull(ContractRules.ValidateMembers([Member(1, chair: true), Member(2, chair: true)]));            // رئيسان
        Assert.NotNull(ContractRules.ValidateMembers([Member(1, chair: true), Member(1)]));                         // موظف مكرر
        Assert.NotNull(ContractRules.ValidateMembers([Member(null, null, chair: true)]));                           // لا موظف ولا اسم
        Assert.NotNull(ContractRules.ValidateMembers([Member(1, "اسم", chair: true)]));                             // الاثنان معاً
        Assert.NotNull(ContractRules.ValidateMembers([Member(1, chair: true, capacity: " ")]));                    // بلا صفة
        Assert.NotNull(ContractRules.ValidateMembers(Enumerable.Range(1, ContractRules.MaxCommitteeMembers + 1).Select(i => Member(i, chair: i == 1)).ToList()));
    }

    [Fact]
    public void The_committee_formation_date_is_not_in_the_future_and_the_text_is_bounded()
    {
        Assert.Null(ContractRules.ValidateCommittee("قرار 5", "توصيات", Today, Today));
        Assert.NotNull(ContractRules.ValidateCommittee(null, null, Today.AddDays(1), Today));
        Assert.NotNull(ContractRules.ValidateCommittee(null, null, Today.AddYears(-31), Today));
        Assert.NotNull(ContractRules.ValidateCommittee(new string('1', 101), null, Today, Today));
        Assert.NotNull(ContractRules.ValidateCommittee(null, new string('أ', 4001), Today, Today));
    }

    [Fact]
    public void Managing_the_committee_needs_its_permission_and_a_contract_that_is_not_terminated_or_cancelled()
    {
        var manage = ViewerOf(UserOf(2), AppPermissions.ManageContractCommittees);
        ContractRules.EnsureCanManageCommittee(manage, ContractOf(ProjectOf()));
        ContractRules.EnsureCanManageCommittee(manage, ContractOf(ProjectOf(), ContractStatus.Active));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanManageCommittee(manage, ContractOf(ProjectOf(), ContractStatus.Cancelled)));
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanManageCommittee(ViewerOf(UserOf(2), AppPermissions.EditContract), ContractOf(ProjectOf())));
    }

    // ───────── المبالغ المحتجزة (أرقام يدوية وقرار موثّق) ─────────

    private static RetentionEntry Entry(decimal amount = 100m, DateTime? withheld = null, params RetentionDecision[] decisions) =>
        new() { Id = 1, Amount = amount, WithheldAt = withheld ?? Today.AddDays(-30), Decisions = decisions.ToList() };

    private static RetentionDecision Decision(int id, RetentionDecisionKind kind, DateTime at) => new() { Id = id, Decision = kind, DecidedAt = at, ApprovingAuthority = "ج" };

    [Fact]
    public void Retention_is_recorded_by_hand_on_an_active_or_completed_contract_with_a_positive_amount()
    {
        var viewer = ViewerOf(UserOf(2), AppPermissions.RecordContractRetention);
        ContractRules.EnsureCanRecordRetention(viewer, ContractOf(ProjectOf(), ContractStatus.Active), 5_000m, Today, null, Today);
        ContractRules.EnsureCanRecordRetention(viewer, ContractOf(ProjectOf(), ContractStatus.Completed), 5_000m, Today.AddDays(-9), "ملاحظة", Today);

        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanRecordRetention(viewer, ContractOf(ProjectOf()), 5m, Today, null, Today));                    // قيد التعاقد
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanRecordRetention(viewer, ContractOf(ProjectOf(), ContractStatus.Active), 0m, Today, null, Today));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanRecordRetention(viewer, ContractOf(ProjectOf(), ContractStatus.Active), 5m, Today.AddDays(1), null, Today));
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanRecordRetention(ViewerOf(UserOf(2), AppPermissions.EditContract), ContractOf(ProjectOf(), ContractStatus.Active), 5m, Today, null, Today));
    }

    [Fact]
    public void The_retention_state_comes_from_the_latest_decision_and_the_total_still_withheld_is_a_plain_sum()
    {
        Assert.Equal(ContractRules.RetentionState.Withheld, ContractRules.StateOf(Entry()));
        Assert.Equal(ContractRules.RetentionState.Continued, ContractRules.StateOf(Entry(100m, null, Decision(1, RetentionDecisionKind.Continue, Today.AddDays(-10)))));
        Assert.Equal(ContractRules.RetentionState.Released, ContractRules.StateOf(Entry(100m, null,
            Decision(1, RetentionDecisionKind.Continue, Today.AddDays(-10)), Decision(2, RetentionDecisionKind.Release, Today.AddDays(-2)))));
        // الأحدث بالتاريخ لا بالإدراج
        Assert.Equal(ContractRules.RetentionState.Continued, ContractRules.StateOf(Entry(100m, null,
            Decision(5, RetentionDecisionKind.Release, Today.AddDays(-10)), Decision(2, RetentionDecisionKind.Continue, Today.AddDays(-2)))));

        var contract = ContractOf(ProjectOf(), ContractStatus.Active);
        contract.RetentionEntries.Add(Entry(100m));
        contract.RetentionEntries.Add(Entry(250m, null, Decision(1, RetentionDecisionKind.Continue, Today)));
        contract.RetentionEntries.Add(Entry(900m, null, Decision(2, RetentionDecisionKind.Release, Today)));
        Assert.Equal(350m, ContractRules.StillWithheld(contract));
    }

    [Fact]
    public void A_retention_decision_needs_the_authority_a_sane_date_and_a_release_is_final()
    {
        var viewer = ViewerOf(UserOf(2), AppPermissions.RecordContractRetention);
        var contract = ContractOf(ProjectOf(), ContractStatus.Completed);
        var entry = Entry(withheld: Today.AddDays(-30));

        ContractRules.EnsureCanDecideRetention(viewer, contract, entry, RetentionDecisionKind.Continue, Today, "وزارة الإسكان", null, Today);
        ContractRules.EnsureCanDecideRetention(viewer, contract, entry, RetentionDecisionKind.Release, Today.AddDays(-30), "وزارة الإسكان", "ملاحظة", Today);

        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanDecideRetention(viewer, contract, entry, RetentionDecisionKind.Release, Today, " ", null, Today));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanDecideRetention(viewer, contract, entry, RetentionDecisionKind.Release, Today.AddDays(1), "ج", null, Today));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanDecideRetention(viewer, contract, entry, RetentionDecisionKind.Release, Today.AddDays(-31), "ج", null, Today));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanDecideRetention(viewer, contract, entry, (RetentionDecisionKind)9, Today, "ج", null, Today));

        var released = Entry(withheld: Today.AddDays(-30), decisions: Decision(1, RetentionDecisionKind.Release, Today.AddDays(-1)));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanDecideRetention(viewer, contract, released, RetentionDecisionKind.Continue, Today, "ج", null, Today));

        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanDecideRetention(ViewerOf(UserOf(2), AppPermissions.EditContract), contract, entry, RetentionDecisionKind.Release, Today, "ج", null, Today));
    }

    // ───────── الضمان: تنبيه لا قرار ─────────

    [Fact]
    public void A_warranty_ending_within_the_window_is_a_notice_candidate_and_nothing_changes_by_time_alone()
    {
        var contract = ContractOf(ProjectOf(), ContractStatus.Completed);
        contract.WarrantyEnd = Today.AddDays(20);

        Assert.True(ContractRules.IsWarrantyEndingSoon(contract, Today, 30));
        Assert.False(ContractRules.IsWarrantyEndingSoon(contract, Today, 10));                      // خارج النافذة
        Assert.True(ContractRules.IsWarrantyEndingSoon(contract, Today.AddDays(20), 30));           // يوم الانتهاء نفسه
        Assert.False(ContractRules.IsWarrantyEndingSoon(contract, Today.AddDays(21), 30));          // انتهى: ليس «قريباً»
        Assert.Equal(20, ContractRules.WarrantyDaysLeft(contract, Today));
        Assert.Equal(-1, ContractRules.WarrantyDaysLeft(contract, Today.AddDays(21)));
        Assert.Null(ContractRules.WarrantyDaysLeft(new Contract(), Today));

        contract.Status = ContractStatus.Terminated;
        Assert.False(ContractRules.IsWarrantyEndingSoon(contract, Today, 30));
        contract.Status = ContractStatus.Active;
        contract.WarrantyEnd = null;
        Assert.False(ContractRules.IsWarrantyEndingSoon(contract, Today, 30));
    }

    [Fact]
    public void One_notice_per_warranty_end_and_changing_the_end_allows_a_new_one()
    {
        var contract = ContractOf(ProjectOf(), ContractStatus.Active);
        Assert.False(ContractRules.NeedsWarrantyNotice(contract));                      // لا نهاية محدَّدة
        contract.WarrantyEnd = Today.AddDays(10);
        Assert.True(ContractRules.NeedsWarrantyNotice(contract));
        contract.WarrantyNotifiedFor = contract.WarrantyEnd;
        Assert.False(ContractRules.NeedsWarrantyNotice(contract));
        contract.WarrantyEnd = Today.AddDays(40);                                        // مُدّد الضمان
        Assert.True(ContractRules.NeedsWarrantyNotice(contract));
    }

    // ───────── تقييم المقاول ─────────

    private static ContractorEvaluation EvaluationOf(int evaluator = 5, EvaluationStatus status = EvaluationStatus.Draft, params decimal?[] scores) => new()
    {
        Id = 1, ContractId = 5, ContractorId = 3, EvaluatedByUserId = evaluator, Status = status,
        Scores = scores.Select((s, i) => new EvaluationScore { Id = i + 1, CriterionId = i + 1, CriterionName = "م" + i, Score = s }).ToList()
    };

    [Fact]
    public void Only_a_project_member_with_the_permission_evaluates_a_contractor_on_an_active_or_completed_contract()
    {
        var engineer = UserOf(5);
        var project = ProjectOf(ProjectStage.Execution, engineer);
        var eval = ViewerOf(engineer, AppPermissions.EvaluateContractor);

        ContractRules.EnsureCanEvaluate(eval, ContractOf(project, ContractStatus.Active));
        ContractRules.EnsureCanEvaluate(eval, ContractOf(project, ContractStatus.Completed));
        ContractRules.EnsureCanEvaluate(ViewerOf(UserOf(1, "SuperAdmin"), AppPermissions.EvaluateContractor), ContractOf(project, ContractStatus.Active));   // مدير النظام بلا مشاركة

        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanEvaluate(ViewerOf(UserOf(6), AppPermissions.EvaluateContractor), ContractOf(project, ContractStatus.Active)));   // ليس مشاركاً
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanEvaluate(ViewerOf(engineer, AppPermissions.ViewContracts), ContractOf(project, ContractStatus.Active)));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanEvaluate(eval, ContractOf(project)));                                                       // قيد التعاقد
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanEvaluate(eval, ContractOf(project, ContractStatus.Terminated)));
        var noContractor = ContractOf(project, ContractStatus.Active);
        noContractor.ContractorId = null;
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanEvaluate(eval, noContractor));
    }

    [Fact]
    public void Scores_are_optional_manual_numbers_between_0_and_100_on_active_criteria_of_the_branch()
    {
        var criteria = new Dictionary<int, EvaluationCriterion>
        {
            [1] = new() { Id = 1, BranchId = Branch, Name = "الجودة", IsActive = true },
            [2] = new() { Id = 2, BranchId = Branch, Name = "الالتزام", IsActive = true },
            [3] = new() { Id = 3, BranchId = 2, Name = "غريب", IsActive = true },
            [4] = new() { Id = 4, BranchId = Branch, Name = "معطل", IsActive = false }
        };

        Assert.Null(ContractRules.ValidateScores([new(1, 80m, null), new(2, null, "لم يُقيَّم")], criteria, Branch));
        Assert.Null(ContractRules.ValidateScores([], criteria, Branch));
        Assert.NotNull(ContractRules.ValidateScores([new(1, 101m, null)], criteria, Branch));
        Assert.NotNull(ContractRules.ValidateScores([new(1, -1m, null)], criteria, Branch));
        Assert.NotNull(ContractRules.ValidateScores([new(1, 5m, null), new(1, 6m, null)], criteria, Branch));       // معيار مكرر
        Assert.NotNull(ContractRules.ValidateScores([new(9, 5m, null)], criteria, Branch));                          // معيار غير موجود
        Assert.NotNull(ContractRules.ValidateScores([new(3, 5m, null)], criteria, Branch));                          // فرع آخر
        Assert.NotNull(ContractRules.ValidateScores([new(4, 5m, null)], criteria, Branch));                          // معطَّل
        Assert.NotNull(ContractRules.ValidateScores([new(1, 5m, new string('أ', 501))], criteria, Branch));
    }

    [Fact]
    public void A_draft_is_edited_and_submitted_by_its_author_only_and_needs_at_least_one_score()
    {
        var engineer = UserOf(5);
        var project = ProjectOf(ProjectStage.Execution, engineer, UserOf(6));
        var contract = ContractOf(project, ContractStatus.Active);
        var author = ViewerOf(engineer, AppPermissions.EvaluateContractor);

        ContractRules.EnsureCanEditEvaluation(author, EvaluationOf());
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanEditEvaluation(ViewerOf(UserOf(6), AppPermissions.EvaluateContractor), EvaluationOf()));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanEditEvaluation(author, EvaluationOf(status: EvaluationStatus.Submitted)));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanEditEvaluation(author, EvaluationOf(status: EvaluationStatus.Reviewed)));

        ContractRules.EnsureCanSubmitEvaluation(author, contract, EvaluationOf(5, EvaluationStatus.Draft, 70m, null));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanSubmitEvaluation(author, contract, EvaluationOf()));                       // بلا درجات
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanSubmitEvaluation(author, contract, EvaluationOf(5, EvaluationStatus.Draft, null, null)));
    }

    [Fact]
    public void Whoever_prepared_the_evaluation_does_not_review_it_not_even_the_system_admin()
    {
        var engineer = UserOf(5);
        var project = ProjectOf(ProjectStage.Execution, engineer);
        var contract = ContractOf(project, ContractStatus.Active);
        var submitted = EvaluationOf(5, EvaluationStatus.Submitted, 80m);

        ContractRules.EnsureCanReviewEvaluation(ViewerOf(UserOf(7), AppPermissions.ReviewContractorEvaluation), contract, submitted, null);
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanReviewEvaluation(ViewerOf(engineer, AppPermissions.ReviewContractorEvaluation), contract, submitted, null));
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanReviewEvaluation(ViewerOf(UserOf(5, "SuperAdmin"), AppPermissions.ReviewContractorEvaluation), contract, submitted, null));
        Assert.Throws<UnauthorizedAccessException>(() => ContractRules.EnsureCanReviewEvaluation(ViewerOf(UserOf(7), AppPermissions.EvaluateContractor), contract, submitted, null));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanReviewEvaluation(ViewerOf(UserOf(7), AppPermissions.ReviewContractorEvaluation), contract, EvaluationOf(5, EvaluationStatus.Draft, 80m), null));
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanReviewEvaluation(ViewerOf(UserOf(7), AppPermissions.ReviewContractorEvaluation), contract, EvaluationOf(5, EvaluationStatus.Reviewed, 80m), null));
    }

    [Fact]
    public void Returning_an_evaluation_to_its_author_needs_a_reason()
    {
        var contract = ContractOf(ProjectOf(ProjectStage.Execution, UserOf(5)), ContractStatus.Active);
        var submitted = EvaluationOf(5, EvaluationStatus.Submitted, 80m);
        var reviewer = ViewerOf(UserOf(7), AppPermissions.ReviewContractorEvaluation);

        ContractRules.EnsureCanReturnEvaluation(reviewer, contract, submitted, "أكمل معيار الجودة");
        Assert.Throws<InvalidOperationException>(() => ContractRules.EnsureCanReturnEvaluation(reviewer, contract, submitted, " "));
    }
}
