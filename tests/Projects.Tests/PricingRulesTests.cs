using Application.Common;
using Application.Features.Pricing;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>
/// قواعد مكتب التسعير: حدّ الفرع، مقدّم الطلب والمكلَّف، لا مقارنة بين وحدات أو عملات مختلفة، لا اختيار لما لم تتحقق مطابقته ولو كان الأرخص،
/// من أوصى لا يعتمد، ومحضر الاتفاق لمدير المكتب وحده ولا يعتمده من أعدّه (حتى مدير النظام).
/// </summary>
public class PricingRulesTests
{
    private const int Branch = 1;
    private static readonly DateTime Today = new(2026, 10, 31);

    private static User UserOf(int id, string role = "Emp", int? branch = Branch, bool active = true) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1, IsActive = active,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = 10
    };

    private static Viewer ViewerOf(User user, params string[] permissions) => new(user, new HashSet<string>(permissions));

    private static PricingRequestItem Item(int id, string unit = "م3", decimal qty = 10m) => new() { Id = id, Description = "بند " + id, Quantity = qty, Unit = unit };

    private static PricingOfferLine Line(int id, int itemId, decimal price, string unit = "م3", ConformityResult conformity = ConformityResult.Conforming, bool selected = false) =>
        new() { Id = id, PricingRequestItemId = itemId, UnitPrice = price, Unit = unit, Conformity = conformity, IsSelected = selected };

    private static PricingOffer Offer(int id, int supplier, string currency = "SYP", DateTime? validUntil = null, params PricingOfferLine[] lines)
    {
        var offer = new PricingOffer { Id = id, SupplierId = supplier, Currency = currency, ValidUntil = validUntil, Lines = lines.ToList() };
        foreach (var l in lines) { l.PricingOffer = offer; l.PricingOfferId = id; }
        return offer;
    }

    private static PricingRequest RequestOf(PricingRequestStatus status = PricingRequestStatus.Collecting, int? assignee = 5, int registeredBy = 2,
        IEnumerable<PricingRequestItem>? items = null, IEnumerable<PricingOffer>? offers = null) => new()
    {
        Id = 9, BranchId = Branch, Title = "طلب", Status = status, AssigneeUserId = assignee, RegisteredByUserId = registeredBy,
        Items = (items ?? [Item(1)]).ToList(), Offers = (offers ?? []).ToList()
    };

    private static readonly Viewer Assignee = ViewerOf(UserOf(5), AppPermissions.PreparePricingOffers);
    private static Contractor SupplierOf(int id, int branch = Branch, bool active = true) => new() { Id = id, BranchId = branch, IsActive = active };

    // ───────── الأسماء والرؤية ─────────

    [Fact]
    public void Internal_numbers_follow_the_creation_year_and_pad_the_id()
    {
        Assert.Equal("PRC-2026-00042", PricingRules.Number(42, new DateTime(2026, 3, 1)));
        Assert.Equal("AGR-2026-00007", PricingRules.AgreementNumber(7, new DateTime(2026, 3, 1)));
    }

    [Fact]
    public void A_request_is_seen_by_its_branch_viewers_its_registrant_and_its_assignee_only()
    {
        var request = RequestOf(registeredBy: 2, assignee: 5);

        PricingRules.EnsureCanView(ViewerOf(UserOf(20), AppPermissions.ViewPricingRequests), request);                         // فرعه
        PricingRules.EnsureCanView(ViewerOf(UserOf(2), AppPermissions.RegisterPricingRequest), request);                         // مقدّمه
        PricingRules.EnsureCanView(ViewerOf(UserOf(5), AppPermissions.PreparePricingOffers), request);                           // المكلَّف
        PricingRules.EnsureCanView(ViewerOf(UserOf(30, branch: 2), AppPermissions.ViewPricingRequests, AppPermissions.ViewOrganizationDashboard), request);   // نظرة المؤسسة

        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanView(ViewerOf(UserOf(30, branch: 2), AppPermissions.ViewPricingRequests), request));   // فرع آخر
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanView(ViewerOf(UserOf(21), AppPermissions.RegisterPricingRequest), request));          // موظف آخر
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanView(ViewerOf(UserOf(20)), request));                                                  // بلا أي صلاحية

        Assert.True(PricingRules.Visible(ViewerOf(UserOf(5), AppPermissions.PreparePricingOffers)).Compile()(request));
        Assert.False(PricingRules.Visible(ViewerOf(UserOf(22), AppPermissions.RegisterPricingRequest)).Compile()(request));
    }

    // ───────── الطلب وبنوده ─────────

    [Fact]
    public void A_request_needs_a_title_and_valid_items_with_quantity_and_unit()
    {
        PricingRules.ItemInput Ok() => new("إسمنت", "درجة 42", 5m, "طن");
        Assert.Null(PricingRules.ValidateRequest("طلب", null, Today.AddDays(5), [Ok()], Today));

        Assert.NotNull(PricingRules.ValidateRequest(" ", null, null, [Ok()], Today));
        Assert.NotNull(PricingRules.ValidateRequest(new string('أ', 201), null, null, [Ok()], Today));
        Assert.NotNull(PricingRules.ValidateRequest("طلب", null, Today.AddYears(-2), [Ok()], Today));
        Assert.NotNull(PricingRules.ValidateRequest("طلب", null, null, [], Today));
        Assert.NotNull(PricingRules.ValidateRequest("طلب", null, null, [Ok() with { Quantity = 0m }], Today));
        Assert.NotNull(PricingRules.ValidateRequest("طلب", null, null, [Ok() with { Unit = " " }], Today));
        Assert.NotNull(PricingRules.ValidateRequest("طلب", null, null, [Ok() with { Description = "" }], Today));
        Assert.NotNull(PricingRules.ValidateRequest("طلب", null, null, Enumerable.Range(0, PricingRules.MaxItems + 1).Select(_ => Ok()).ToList(), Today));
    }

    [Fact]
    public void Registering_needs_the_permission_a_visible_open_project_and_a_contract_of_that_project()
    {
        var reg = ViewerOf(UserOf(2), AppPermissions.RegisterPricingRequest, AppPermissions.ViewBranchProjects);
        var project = new Project { Id = 1, BranchId = Branch, Stage = ProjectStage.Execution, CreatedByUserId = 99 };

        PricingRules.EnsureCanRegister(reg, null, null);                                   // بلا مشروع
        PricingRules.EnsureCanRegister(reg, project, new Contract { ProjectId = 1, Status = ContractStatus.Active });

        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanRegister(ViewerOf(UserOf(2)), null, null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanRegister(ViewerOf(UserOf(3, branch: 2), AppPermissions.RegisterPricingRequest, AppPermissions.ViewBranchProjects), project, null));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanRegister(reg, new Project { Id = 1, BranchId = Branch, Stage = ProjectStage.Closed }, null));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanRegister(reg, project, new Contract { ProjectId = 2, Status = ContractStatus.Active }));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanRegister(reg, null, new Contract { ProjectId = 1, Status = ContractStatus.Active }));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanRegister(reg, project, new Contract { ProjectId = 1, Status = ContractStatus.Terminated }));
    }

    [Fact]
    public void The_registrant_edits_only_before_assignment_and_the_manager_while_the_request_is_open()
    {
        var registrant = ViewerOf(UserOf(2), AppPermissions.RegisterPricingRequest);
        var manager = ViewerOf(UserOf(8), AppPermissions.AssignPricingRequest);

        PricingRules.EnsureCanEdit(registrant, RequestOf(PricingRequestStatus.Registered, assignee: null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanEdit(registrant, RequestOf(PricingRequestStatus.Collecting)));
        PricingRules.EnsureCanEdit(manager, RequestOf(PricingRequestStatus.Collecting));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanEdit(ViewerOf(UserOf(8, branch: 2), AppPermissions.AssignPricingRequest), RequestOf()));   // فرع آخر
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanEdit(manager, RequestOf(PricingRequestStatus.Recommended)));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanEdit(manager, RequestOf(PricingRequestStatus.Approved)));
    }

    [Fact]
    public void Items_freeze_once_the_first_offer_is_recorded()
    {
        PricingRules.EnsureItemsEditable(RequestOf());
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureItemsEditable(RequestOf(offers: [Offer(1, 3)])));
    }

    [Fact]
    public void Assigning_needs_the_permission_an_active_branch_employee_who_can_prepare_offers()
    {
        var manager = ViewerOf(UserOf(8), AppPermissions.AssignPricingRequest);
        var request = RequestOf(PricingRequestStatus.Registered, assignee: null);

        PricingRules.EnsureCanAssign(manager, request, UserOf(5), true);
        PricingRules.EnsureCanAssign(manager, RequestOf(PricingRequestStatus.Collecting), UserOf(6), true);          // تغيير المكلَّف

        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanAssign(ViewerOf(UserOf(8)), request, UserOf(5), true));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanAssign(manager, request, UserOf(5), false));                // بلا صلاحية الإعداد
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanAssign(manager, request, UserOf(5, active: false), true));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanAssign(manager, request, UserOf(5, branch: 2), true));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanAssign(manager, RequestOf(PricingRequestStatus.Recommended), UserOf(5), true));
    }

    [Fact]
    public void Cancelling_is_by_the_manager_or_the_registrant_before_assignment_and_never_after_the_recommendation()
    {
        var manager = ViewerOf(UserOf(8), AppPermissions.AssignPricingRequest);
        PricingRules.EnsureCanCancel(manager, RequestOf());
        PricingRules.EnsureCanCancel(ViewerOf(UserOf(2), AppPermissions.RegisterPricingRequest), RequestOf(PricingRequestStatus.Registered, assignee: null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanCancel(ViewerOf(UserOf(2), AppPermissions.RegisterPricingRequest), RequestOf()));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanCancel(manager, RequestOf(PricingRequestStatus.Recommended)));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanCancel(manager, RequestOf(PricingRequestStatus.Approved)));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanCancel(manager, RequestOf(PricingRequestStatus.Cancelled)));
    }

    // ───────── العروض ─────────

    [Fact]
    public void Only_the_assignee_works_on_offers_and_only_while_the_request_is_collecting()
    {
        PricingRules.EnsureCanWork(Assignee, RequestOf());
        PricingRules.EnsureCanWork(ViewerOf(UserOf(1, "SuperAdmin"), AppPermissions.PreparePricingOffers), RequestOf());       // مدير النظام

        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanWork(ViewerOf(UserOf(6), AppPermissions.PreparePricingOffers), RequestOf()));     // غير المكلَّف
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanWork(ViewerOf(UserOf(5), AppPermissions.RegisterPricingRequest), RequestOf()));   // بلا الصلاحية
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanWork(Assignee, RequestOf(PricingRequestStatus.Registered, assignee: 5)));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanWork(Assignee, RequestOf(PricingRequestStatus.Recommended)));
    }

    [Fact]
    public void An_offer_needs_a_valid_currency_dates_in_order_and_a_new_active_supplier_of_the_branch()
    {
        Assert.Null(PricingRules.ValidateOffer("ع/1", "SYP", Today, Today.AddDays(30), "تسليم موقع", null, Today));
        Assert.Null(PricingRules.ValidateOffer(null, "USD", Today, null, null, null, Today));
        Assert.NotNull(PricingRules.ValidateOffer(null, "sy", Today, null, null, null, Today));
        Assert.NotNull(PricingRules.ValidateOffer(null, "SYP", Today.AddDays(1), null, null, null, Today));
        Assert.NotNull(PricingRules.ValidateOffer(null, "SYP", Today, Today.AddDays(-1), null, null, Today));
        Assert.NotNull(PricingRules.ValidateOffer(null, "SYP", Today.AddYears(-6), null, null, null, Today));

        var request = RequestOf(offers: [Offer(1, 3)]);
        PricingRules.EnsureCanAddOffer(Assignee, request, SupplierOf(4));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanAddOffer(Assignee, request, SupplierOf(3)));                   // عرض مكرر
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanAddOffer(Assignee, request, SupplierOf(4, branch: 2)));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanAddOffer(Assignee, request, SupplierOf(4, active: false)));
    }

    [Fact]
    public void Line_prices_are_non_negative_per_item_without_duplicates_and_need_the_quoted_unit()
    {
        var request = RequestOf(items: [Item(1), Item(2)]);
        PricingRules.LineInput Ok(int item = 1) => new(item, 100m, "م3", (int)ConformityResult.Conforming, null);

        Assert.Null(PricingRules.ValidateLines(request, [Ok(1), Ok(2)]));
        Assert.NotNull(PricingRules.ValidateLines(request, []));
        Assert.NotNull(PricingRules.ValidateLines(request, [Ok(1), Ok(1)]));
        Assert.NotNull(PricingRules.ValidateLines(request, [Ok(9)]));
        Assert.NotNull(PricingRules.ValidateLines(request, [Ok() with { UnitPrice = -1m }]));
        Assert.NotNull(PricingRules.ValidateLines(request, [Ok() with { Unit = " " }]));
        Assert.NotNull(PricingRules.ValidateLines(request, [Ok() with { Conformity = 9 }]));

        PricingRules.EnsureLineEditable(Line(1, 1, 5m));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureLineEditable(Line(1, 1, 5m, selected: true)));
    }

    // ───────── المقارنة ─────────

    [Fact]
    public void Different_units_are_not_comparable_and_say_why()
    {
        var item = Item(1, "م3");
        var rows = PricingRules.Compare(item, [Offer(1, 3, lines: Line(1, 1, 100m, "م3")), Offer(2, 4, lines: Line(2, 1, 70m, "طن"))]);

        var m3 = rows.Single(r => r.Line.Id == 1);
        var ton = rows.Single(r => r.Line.Id == 2);
        Assert.True(m3.Comparable);
        Assert.False(ton.Comparable);
        Assert.Contains("بلا تحويل معتمد", ton.NotComparableReason);
        Assert.True(m3.IsLowest);                 // الأرخص بين القابل للمقارنة فقط (الأرخص الظاهر 70 بوحدة أخرى لا يُحسب)
        Assert.False(ton.IsLowest);
        Assert.Equal(1000m, m3.Total);            // 100 × 10
        Assert.True(PricingRules.SameUnit(" م3 ", "م3"));
    }

    [Fact]
    public void Currencies_are_compared_separately_and_never_mixed()
    {
        var item = Item(1);
        var rows = PricingRules.Compare(item, [
            Offer(1, 3, "SYP", null, Line(1, 1, 100000m)),
            Offer(2, 4, "SYP", null, Line(2, 1, 90000m)),
            Offer(3, 5, "USD", null, Line(3, 1, 40m))]);

        Assert.Equal(["SYP", "USD"], PricingRules.CurrenciesIn(rows));
        Assert.True(rows.Single(r => r.Line.Id == 2).IsLowest);       // الأرخص بالليرة
        Assert.False(rows.Single(r => r.Line.Id == 1).IsLowest);
        Assert.True(rows.Single(r => r.Line.Id == 3).IsLowest);       // الأرخص بالدولار (الوحيد) — لا يُقارَن بالليرة
    }

    [Fact]
    public void The_lowest_ignores_lines_that_do_not_conform_so_it_is_never_an_automatic_choice()
    {
        var item = Item(1);
        var rows = PricingRules.Compare(item, [
            Offer(1, 3, lines: Line(1, 1, 50m, conformity: ConformityResult.NonConforming)),
            Offer(2, 4, lines: Line(2, 1, 80m)),
            Offer(3, 5, lines: Line(3, 1, 60m, conformity: ConformityResult.NotEvaluated))]);

        Assert.True(rows.Single(r => r.Line.Id == 2).IsLowest);
        Assert.False(rows.Single(r => r.Line.Id == 1).IsLowest);
        Assert.False(rows.Single(r => r.Line.Id == 3).IsLowest);

        Assert.Empty(PricingRules.Compare(Item(7), [Offer(1, 3, lines: Line(1, 1, 5m))]));      // لا أسعار لهذا البند
    }

    // ───────── الاختيار والتوصية ─────────

    [Fact]
    public void A_line_is_selected_only_if_it_conforms_has_the_item_unit_is_valid_and_comes_with_a_reason()
    {
        var item = Item(1);
        var good = Line(1, 1, 100m);
        var offer = Offer(1, 3, lines: good);
        var request = RequestOf(items: [item], offers: [offer]);

        PricingRules.EnsureCanSelect(Assignee, request, item, offer, good, "الأنسب فنياً", Today);

        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSelect(Assignee, request, item, offer, good, " ", Today));              // بلا سبب
        var bad = Line(2, 1, 10m, conformity: ConformityResult.NonConforming);                                                                         // الأرخص لكن غير مطابق
        var badOffer = Offer(2, 4, lines: bad);
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSelect(Assignee, request, item, badOffer, bad, "الأرخص", Today));
        var unevaluated = Line(3, 1, 10m, conformity: ConformityResult.NotEvaluated);
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSelect(Assignee, request, item, Offer(3, 5, lines: unevaluated), unevaluated, "س", Today));
        var otherUnit = Line(4, 1, 10m, "طن");
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSelect(Assignee, request, item, Offer(4, 6, lines: otherUnit), otherUnit, "س", Today));
        var expired = Line(5, 1, 10m);
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSelect(Assignee, request, item, Offer(5, 7, "SYP", Today.AddDays(-1), expired), expired, "س", Today));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSelect(Assignee, request, Item(2), offer, good, "س", Today));            // بند آخر
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanSelect(ViewerOf(UserOf(6), AppPermissions.PreparePricingOffers), request, item, offer, good, "س", Today));
    }

    [Fact]
    public void Unselecting_needs_a_selected_line_and_the_assignee()
    {
        var request = RequestOf();
        PricingRules.EnsureCanUnselect(Assignee, request, Line(1, 1, 5m, selected: true));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanUnselect(Assignee, request, Line(1, 1, 5m)));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanUnselect(ViewerOf(UserOf(6), AppPermissions.PreparePricingOffers), request, Line(1, 1, 5m, selected: true)));
    }

    [Fact]
    public void The_recommendation_needs_a_selection_for_every_item_an_offer_and_its_text()
    {
        var two = new[] { Item(1), Item(2) };
        var selectedBoth = RequestOf(items: two, offers: [Offer(1, 3, lines: [Line(1, 1, 5m, selected: true), Line(2, 2, 6m, selected: true)])]);
        PricingRules.EnsureCanRecommend(Assignee, selectedBoth, "نوصي بعرض المورد");

        var missingOne = RequestOf(items: two, offers: [Offer(1, 3, lines: [Line(1, 1, 5m, selected: true), Line(2, 2, 6m)])]);
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanRecommend(Assignee, missingOne, "توصية"));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanRecommend(Assignee, RequestOf(), "توصية"));                          // بلا عروض
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanRecommend(Assignee, selectedBoth, " "));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanRecommend(Assignee, selectedBoth, new string('أ', 2001)));
    }

    [Fact]
    public void Selected_totals_are_simple_sums_per_currency_and_currencies_are_never_added_together()
    {
        var items = new[] { Item(1, qty: 10m), Item(2, qty: 4m) };
        var request = RequestOf(items: items, offers: [
            Offer(1, 3, "SYP", null, Line(1, 1, 100m, selected: true)),
            Offer(2, 4, "USD", null, Line(2, 2, 25m, selected: true)),
            Offer(3, 5, "SYP", null, Line(3, 2, 1m))]);

        var totals = PricingRules.SelectedTotals(request);
        Assert.Equal(1000m, totals["SYP"]);
        Assert.Equal(100m, totals["USD"]);
        Assert.Equal(2, totals.Count);
    }

    // ───────── الاعتماد والإرسال ─────────

    [Fact]
    public void Whoever_recommended_does_not_approve_not_even_the_system_admin()
    {
        var request = RequestOf(PricingRequestStatus.Recommended);
        request.RecommendedByUserId = 5;
        var approver = ViewerOf(UserOf(8), AppPermissions.ApprovePricingRequest);

        PricingRules.EnsureCanDecide(approver, request, PricingRules.Decision.Approve, null);
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanDecide(ViewerOf(UserOf(5), AppPermissions.ApprovePricingRequest), request, PricingRules.Decision.Approve, null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanDecide(ViewerOf(UserOf(5, "SuperAdmin"), AppPermissions.ApprovePricingRequest), request, PricingRules.Decision.Approve, null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanDecide(ViewerOf(UserOf(8)), request, PricingRules.Decision.Approve, null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanDecide(ViewerOf(UserOf(8, branch: 2), AppPermissions.ApprovePricingRequest), request, PricingRules.Decision.Approve, null));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanDecide(approver, RequestOf(), PricingRules.Decision.Approve, null));          // ليس بانتظار الاعتماد
    }

    [Fact]
    public void Rejecting_or_returning_needs_a_reason_and_each_decision_moves_the_request_to_its_status()
    {
        var request = RequestOf(PricingRequestStatus.Recommended);
        request.RecommendedByUserId = 5;
        var approver = ViewerOf(UserOf(8), AppPermissions.ApprovePricingRequest);

        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanDecide(approver, request, PricingRules.Decision.Reject, " "));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanDecide(approver, request, PricingRules.Decision.Return, null));
        PricingRules.EnsureCanDecide(approver, request, PricingRules.Decision.Reject, "السعر مرتفع");
        PricingRules.EnsureCanDecide(approver, request, PricingRules.Decision.Return, "أضف عرضاً ثالثاً");
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanDecide(approver, request, (PricingRules.Decision)9, "x"));

        Assert.Equal(PricingRequestStatus.Approved, PricingRules.StatusAfter(PricingRules.Decision.Approve));
        Assert.Equal(PricingRequestStatus.Rejected, PricingRules.StatusAfter(PricingRules.Decision.Reject));
        Assert.Equal(PricingRequestStatus.Collecting, PricingRules.StatusAfter(PricingRules.Decision.Return));
    }

    [Fact]
    public void The_result_is_sent_after_a_decision_to_a_named_party_by_its_permission_holder()
    {
        var sender = ViewerOf(UserOf(9), AppPermissions.SendPricingResult);
        PricingRules.EnsureCanSendResult(sender, RequestOf(PricingRequestStatus.Approved), "مهندس الموقع", "بالبريد");
        PricingRules.EnsureCanSendResult(sender, RequestOf(PricingRequestStatus.Rejected), "مكتب العقود", null);

        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSendResult(sender, RequestOf(PricingRequestStatus.Recommended), "س", null));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSendResult(sender, RequestOf(PricingRequestStatus.ResultSent), "س", null));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSendResult(sender, RequestOf(PricingRequestStatus.Approved), " ", null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanSendResult(ViewerOf(UserOf(9)), RequestOf(PricingRequestStatus.Approved), "س", null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanSendResult(ViewerOf(UserOf(9, branch: 2), AppPermissions.SendPricingResult), RequestOf(PricingRequestStatus.Approved), "س", null));
    }

    [Fact]
    public void An_overdue_request_is_a_notice_candidate_once_per_due_date_and_nothing_changes_by_time_alone()
    {
        var request = RequestOf();
        request.DueDate = Today.AddDays(-2);
        Assert.True(PricingRules.IsOverdueCandidate(request, Today));
        Assert.True(PricingRules.NeedsOverdueNotice(request));
        request.OverdueNotifiedFor = request.DueDate;
        Assert.False(PricingRules.NeedsOverdueNotice(request));
        request.DueDate = Today.AddDays(-1);
        Assert.True(PricingRules.NeedsOverdueNotice(request));              // الموعد تغيّر

        request.DueDate = Today;
        Assert.False(PricingRules.IsOverdueCandidate(request, Today));       // اليوم ليس تجاوزاً
        request.DueDate = Today.AddDays(-2);
        foreach (var status in new[] { PricingRequestStatus.Approved, PricingRequestStatus.Rejected, PricingRequestStatus.ResultSent, PricingRequestStatus.Cancelled })
        {
            request.Status = status;
            Assert.False(PricingRules.IsOverdueCandidate(request, Today));
        }
        request.Status = PricingRequestStatus.Recommended;
        Assert.True(PricingRules.IsOverdueCandidate(request, Today));
    }

    // ───────── محضر الاتفاق ─────────

    private static PricingAgreement AgreementOf(AgreementStatus status = AgreementStatus.Draft, int preparedBy = 8, string? number = "م/5", int lines = 1) => new()
    {
        Id = 4, BranchId = Branch, Status = status, PreparedByUserId = preparedBy, Number = number,
        Lines = Enumerable.Range(1, lines).Select(i => new PricingAgreementLine { Id = i, Description = "بند", Quantity = 2m, Unit = "م2", UnitPrice = 5m }).ToList()
    };

    private static readonly Viewer Manager = ViewerOf(UserOf(8), AppPermissions.PreparePricingAgreement);

    [Fact]
    public void An_agreement_needs_valid_data_and_at_least_one_priced_line()
    {
        PricingRules.AgreementLineInput Ok() => new("توريد بلاط", 100m, "م2", 12000m);
        Assert.Null(PricingRules.ValidateAgreement("م/1", Today, "مواصفات", "طريقة", "SYP", null, [Ok()], Today));

        Assert.NotNull(PricingRules.ValidateAgreement(new string('1', 101), Today, null, null, "SYP", null, [Ok()], Today));
        Assert.NotNull(PricingRules.ValidateAgreement(null, Today.AddDays(1), null, null, "SYP", null, [Ok()], Today));
        Assert.NotNull(PricingRules.ValidateAgreement(null, Today, null, null, "syp", null, [Ok()], Today));
        Assert.NotNull(PricingRules.ValidateAgreement(null, Today, null, null, "SYP", null, [], Today));
        Assert.NotNull(PricingRules.ValidateAgreement(null, Today, null, null, "SYP", null, [Ok() with { Quantity = 0m }], Today));
        Assert.NotNull(PricingRules.ValidateAgreement(null, Today, null, null, "SYP", null, [Ok() with { UnitPrice = -1m }], Today));
        Assert.NotNull(PricingRules.ValidateAgreement(null, Today, null, null, "SYP", null, [Ok() with { Unit = "" }], Today));
    }

    [Fact]
    public void Preparing_an_agreement_is_for_the_office_manager_permission_with_an_active_supplier_of_the_branch()
    {
        var project = new Project { Id = 1, BranchId = Branch, Stage = ProjectStage.Execution, CreatedByUserId = 8 };
        var manager = ViewerOf(UserOf(8), AppPermissions.PreparePricingAgreement, AppPermissions.ViewBranchProjects);

        PricingRules.EnsureCanPrepareAgreement(manager, Branch, SupplierOf(3), null);
        PricingRules.EnsureCanPrepareAgreement(manager, Branch, SupplierOf(3), project);

        // الصلاحية وحدها هي القيد: غير الحامل (ولو اعتماداً أو عرضاً) لا يُعِدّ
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanPrepareAgreement(ViewerOf(UserOf(9), AppPermissions.ApprovePricingAgreement, AppPermissions.ViewPricingAgreements), Branch, SupplierOf(3), null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanPrepareAgreement(ViewerOf(UserOf(8, branch: 2), AppPermissions.PreparePricingAgreement), Branch, SupplierOf(3), null));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanPrepareAgreement(manager, Branch, SupplierOf(3, active: false), null));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanPrepareAgreement(manager, Branch, SupplierOf(3, branch: 2), null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanPrepareAgreement(manager, Branch, SupplierOf(3), new Project { Id = 2, BranchId = 2, CreatedByUserId = 8 }));   // خارج حدّ رؤية المشروع
    }

    [Fact]
    public void Only_the_preparer_edits_a_draft_or_returned_agreement_and_an_approved_one_is_frozen()
    {
        PricingRules.EnsureCanEditAgreement(Manager, AgreementOf());
        PricingRules.EnsureCanEditAgreement(Manager, AgreementOf(AgreementStatus.Returned));
        PricingRules.EnsureCanEditAgreement(ViewerOf(UserOf(1, "SuperAdmin"), AppPermissions.PreparePricingAgreement), AgreementOf());

        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanEditAgreement(ViewerOf(UserOf(9), AppPermissions.PreparePricingAgreement), AgreementOf()));    // أعدّه غيره
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanEditAgreement(ViewerOf(UserOf(8)), AgreementOf()));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanEditAgreement(Manager, AgreementOf(AgreementStatus.Submitted)));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanEditAgreement(Manager, AgreementOf(AgreementStatus.Approved)));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanEditAgreement(Manager, AgreementOf(AgreementStatus.Cancelled)));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanCancelAgreement(Manager, AgreementOf(AgreementStatus.Approved)));
    }

    [Fact]
    public void Submitting_needs_the_official_number_and_a_priced_line()
    {
        PricingRules.EnsureCanSubmitAgreement(Manager, AgreementOf());
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSubmitAgreement(Manager, AgreementOf(number: " ")));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanSubmitAgreement(Manager, AgreementOf(lines: 0)));
    }

    [Fact]
    public void Whoever_prepared_the_agreement_does_not_approve_it_not_even_the_system_admin()
    {
        var submitted = AgreementOf(AgreementStatus.Submitted);
        var approver = ViewerOf(UserOf(9), AppPermissions.ApprovePricingAgreement);

        PricingRules.EnsureCanApproveAgreement(approver, submitted, true, null);
        PricingRules.EnsureCanApproveAgreement(approver, submitted, false, "صحّح الأسعار");

        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanApproveAgreement(ViewerOf(UserOf(8), AppPermissions.ApprovePricingAgreement), submitted, true, null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanApproveAgreement(ViewerOf(UserOf(8, "SuperAdmin"), AppPermissions.ApprovePricingAgreement), submitted, true, null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanApproveAgreement(ViewerOf(UserOf(9)), submitted, true, null));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanApproveAgreement(ViewerOf(UserOf(9, branch: 2), AppPermissions.ApprovePricingAgreement), submitted, true, null));
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanApproveAgreement(approver, AgreementOf(), true, null));                         // ليس مرسلاً
        Assert.Throws<InvalidOperationException>(() => PricingRules.EnsureCanApproveAgreement(approver, submitted, false, " "));                             // الإعادة بلا سبب
    }

    [Fact]
    public void Agreement_visibility_follows_the_branch_and_the_agreement_total_is_a_plain_sum()
    {
        var agreement = AgreementOf(lines: 3);
        PricingRules.EnsureCanViewAgreement(ViewerOf(UserOf(20), AppPermissions.ViewPricingAgreements), agreement);
        PricingRules.EnsureCanViewAgreement(ViewerOf(UserOf(30, branch: 2), AppPermissions.ViewPricingAgreements, AppPermissions.ViewOrganizationDashboard), agreement);
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanViewAgreement(ViewerOf(UserOf(30, branch: 2), AppPermissions.ViewPricingAgreements), agreement));
        Assert.Throws<UnauthorizedAccessException>(() => PricingRules.EnsureCanViewAgreement(ViewerOf(UserOf(20)), agreement));
        Assert.Equal(30m, PricingRules.AgreementTotal(agreement));      // 3 × (2 × 5)
    }
}
