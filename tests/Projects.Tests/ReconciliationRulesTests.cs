using Application.Common;
using Application.Features.Reconciliation;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>
/// مطابقة الفواتير: لا مقارنة بين وحدات أو عملات، النتيجة المقترحة معلومة، المدقق لا يعتمد حتى مدير النظام،
/// «مطابق» لا يُسجَّل والمقارنة تخالفه، والمعتمد لا يُعدَّل، والإحالة بعد الاعتماد مرة واحدة.
/// </summary>
public class ReconciliationRulesTests
{
    private const int Branch = 1;
    private static readonly DateTime Today = new(2026, 11, 1);

    private static User UserOf(int id, string role = "Emp", int? branch = Branch) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1, IsActive = true,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = 10
    };

    private static Viewer ViewerOf(User user, params string[] permissions) => new(user, new HashSet<string>(permissions));

    private static PriceReconciliationLine Line(decimal qty = 10m, decimal price = 100m, decimal? refPrice = 100m, decimal? refQty = 10m, string unit = "م", string? refUnit = "م",
        string? refCurrency = "SYP", ReconciliationReferenceKind kind = ReconciliationReferenceKind.OfferLine) => new()
    {
        Description = "بند", Unit = unit, InvoiceQuantity = qty, InvoiceUnitPrice = price, ReferenceKind = kind, ReferenceUnitPrice = refPrice, ReferenceQuantity = refQty,
        ReferenceUnit = refUnit, ReferenceCurrency = refCurrency
    };

    private static PriceReconciliation Rec(ReconciliationStatus status = ReconciliationStatus.Draft, int preparedBy = 2, string currency = "SYP", params PriceReconciliationLine[] lines) => new()
    {
        Id = 5, BranchId = Branch, Status = status, PreparedByUserId = preparedBy, Currency = currency, Lines = lines.Length == 0 ? [Line()] : lines.ToList()
    };

    private static readonly Viewer Auditor = ViewerOf(UserOf(2), AppPermissions.PreparePriceReconciliation);
    private static readonly Viewer Approver = ViewerOf(UserOf(9), AppPermissions.ApprovePriceReconciliation);

    // ───────── الأسماء والمدخلات ─────────

    [Fact]
    public void The_internal_number_follows_the_creation_year_and_pads_the_id()
    {
        Assert.Equal("MAT-2026-00042", ReconciliationRules.Number(42, new DateTime(2026, 3, 1)));
    }

    [Fact]
    public void Only_the_six_audit_results_are_recorded_by_the_auditor()
    {
        foreach (var s in new[] { ReconciliationStatus.Match, ReconciliationStatus.PartialMatch, ReconciliationStatus.PriceDifference, ReconciliationStatus.QuantityDifference, ReconciliationStatus.IncompleteData, ReconciliationStatus.NeedsReview })
            Assert.True(ReconciliationRules.IsAuditResult(s));
        foreach (var s in new[] { ReconciliationStatus.Draft, ReconciliationStatus.ApprovedAfterAudit, ReconciliationStatus.ReturnedForCorrection, ReconciliationStatus.Cancelled })
            Assert.False(ReconciliationRules.IsAuditResult(s));
    }

    private static ReconciliationRules.LineInput In(string? description = "بند", string? unit = "م", decimal qty = 1m, decimal price = 5m, int kind = 1, int? refId = null) => new(description, unit, qty, price, kind, refId, null);

    [Fact]
    public void A_document_needs_a_number_a_valid_date_currency_and_well_formed_lines()
    {
        string? Check(int kind = 1, string? number = "F-1", DateTime? date = null, string? currency = "SYP", params ReconciliationRules.LineInput[] lines) =>
            ReconciliationRules.ValidateDocument(kind, number, date ?? Today.AddDays(-1), currency, null, lines.Length == 0 ? [In()] : lines, Today);

        Assert.Null(Check());
        Assert.NotNull(Check(kind: 9));
        Assert.NotNull(Check(number: " "));
        Assert.NotNull(Check(date: Today.AddDays(1)));
        Assert.NotNull(Check(currency: "syp"));
        Assert.NotNull(ReconciliationRules.ValidateDocument(1, "F", Today, "SYP", null, [], Today));
        Assert.NotNull(Check(lines: In(description: " ")));
        Assert.NotNull(Check(lines: In(unit: "")));
        Assert.NotNull(Check(lines: In(qty: 0m)));
        Assert.NotNull(Check(lines: In(price: -1m)));
        Assert.NotNull(Check(lines: In(kind: 2, refId: null)));      // مرجع بلا معرّف
        Assert.NotNull(Check(lines: In(kind: 7)));
        Assert.Null(Check(lines: In(kind: 3, refId: 4)));
    }

    // ───────── الرؤية والتسجيل ─────────

    [Fact]
    public void Viewing_needs_a_reconciliation_permission_and_the_branch()
    {
        var rec = Rec();
        ReconciliationRules.EnsureCanView(ViewerOf(UserOf(20), AppPermissions.ViewPriceReconciliations), rec);
        ReconciliationRules.EnsureCanView(Auditor, rec);
        ReconciliationRules.EnsureCanView(Approver, rec);
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanView(ViewerOf(UserOf(21)), rec));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanView(ViewerOf(UserOf(22, branch: 99), AppPermissions.ViewPriceReconciliations), rec));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanView(ViewerOf(UserOf(23, branch: null), AppPermissions.ViewPriceReconciliations), rec));
    }

    [Fact]
    public void Preparing_needs_the_permission_the_branch_and_a_usable_supplier()
    {
        var supplier = new Contractor { Id = 7, BranchId = Branch, IsActive = true };
        ReconciliationRules.EnsureCanPrepare(Auditor, Branch, supplier, null);
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanPrepare(Approver, Branch, supplier, null));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanPrepare(Auditor, 99, supplier, null));
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanPrepare(Auditor, Branch, new Contractor { Id = 8, BranchId = Branch, IsActive = false }, null));
    }

    // ───────── المقارنة والاقتراح ─────────

    [Fact]
    public void A_line_without_a_reference_or_with_another_unit_or_currency_is_not_comparable()
    {
        Assert.False(ReconciliationRules.Compare(Line(kind: ReconciliationReferenceKind.None, refPrice: null), "SYP").Comparable);
        var unit = ReconciliationRules.Compare(Line(refUnit: "لفة"), "SYP");
        Assert.False(unit.Comparable);
        Assert.Contains("لا تُقارَن", unit.NotComparableReason);
        Assert.False(ReconciliationRules.Compare(Line(refCurrency: "USD"), "SYP").Comparable);
        Assert.True(ReconciliationRules.Compare(Line(refUnit: " م "), "syp").Comparable);          // مسافات وحالة أحرف لا تفرّق
    }

    [Fact]
    public void Differences_are_invoice_minus_reference_and_quantity_is_skipped_without_a_reference_quantity()
    {
        var c = ReconciliationRules.Compare(Line(qty: 12m, price: 110m, refPrice: 100m, refQty: 10m), "SYP");
        Assert.Equal(10m, c.PriceDifference);
        Assert.Equal(2m, c.QuantityDifference);
        Assert.True(c.HasPriceDifference && c.HasQuantityDifference);

        var noQty = ReconciliationRules.Compare(Line(refQty: null), "SYP");
        Assert.Null(noQty.QuantityDifference);
        Assert.False(noQty.HasQuantityDifference);
        Assert.False(noQty.HasPriceDifference);
    }

    [Fact]
    public void The_suggested_result_follows_the_lines_but_is_only_a_suggestion()
    {
        Assert.Equal(ReconciliationStatus.Match, ReconciliationRules.Suggest(Rec()));
        Assert.Equal(ReconciliationStatus.IncompleteData, ReconciliationRules.Suggest(Rec(lines: [Line(), Line(kind: ReconciliationReferenceKind.None, refPrice: null)])));
        Assert.Equal(ReconciliationStatus.IncompleteData, ReconciliationRules.Suggest(Rec(lines: [Line(refCurrency: "USD")])));
        Assert.Equal(ReconciliationStatus.PartialMatch, ReconciliationRules.Suggest(Rec(lines: [Line(), Line(price: 120m)])));
        Assert.Equal(ReconciliationStatus.PriceDifference, ReconciliationRules.Suggest(Rec(lines: [Line(price: 120m), Line(price: 90m)])));
        Assert.Equal(ReconciliationStatus.QuantityDifference, ReconciliationRules.Suggest(Rec(lines: [Line(qty: 11m)])));
        Assert.Equal(ReconciliationStatus.NeedsReview, ReconciliationRules.Suggest(Rec(lines: [Line(qty: 11m, price: 120m)])));
        Assert.Equal(ReconciliationStatus.IncompleteData, ReconciliationRules.Suggest(new PriceReconciliation { Currency = "SYP", Lines = [] }));      // بلا بنود
    }

    [Fact]
    public void The_total_is_a_plain_sum_of_quantity_times_price()
    {
        Assert.Equal(10m * 100m + 2m * 50m, ReconciliationRules.InvoiceTotal(Rec(lines: [Line(10m, 100m), Line(2m, 50m)])));
    }

    // ───────── التسجيل والاعتماد ─────────

    [Fact]
    public void Recording_needs_the_auditor_a_draft_or_returned_record_and_a_note_unless_it_matches()
    {
        ReconciliationRules.EnsureCanRecord(Auditor, Rec(), ReconciliationStatus.Match, null);
        ReconciliationRules.EnsureCanRecord(Auditor, Rec(lines: [Line(price: 120m)]), ReconciliationStatus.PriceDifference, "فرق 20");
        ReconciliationRules.EnsureCanRecord(Auditor, Rec(ReconciliationStatus.ReturnedForCorrection, lines: [Line(price: 120m)]), ReconciliationStatus.NeedsReview, "راجعوا");

        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRecord(Auditor, Rec(lines: [Line(price: 120m)]), ReconciliationStatus.PriceDifference, " "));
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRecord(Auditor, Rec(), ReconciliationStatus.ApprovedAfterAudit, "س"));       // ليست من الست
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRecord(Auditor, Rec(ReconciliationStatus.PriceDifference), ReconciliationStatus.Match, null));   // مسجَّلة سابقاً
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRecord(Auditor, Rec(ReconciliationStatus.ApprovedAfterAudit), ReconciliationStatus.Match, null));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanRecord(ViewerOf(UserOf(3), AppPermissions.PreparePriceReconciliation), Rec(preparedBy: 2), ReconciliationStatus.Match, null));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanRecord(Approver, Rec(), ReconciliationStatus.Match, null));
    }

    [Fact]
    public void A_match_cannot_be_recorded_when_the_comparison_says_otherwise()
    {
        var withDifference = Rec(lines: [Line(price: 120m)]);
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRecord(Auditor, withDifference, ReconciliationStatus.Match, null));
        var incomplete = Rec(lines: [Line(kind: ReconciliationReferenceKind.None, refPrice: null)]);
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRecord(Auditor, incomplete, ReconciliationStatus.Match, "لا فرق"));
        ReconciliationRules.EnsureCanRecord(Auditor, incomplete, ReconciliationStatus.IncompleteData, "لا مرجع");
    }

    [Fact]
    public void Whoever_audited_does_not_approve_even_the_super_admin()
    {
        var recorded = Rec(ReconciliationStatus.PriceDifference, preparedBy: 2);
        ReconciliationRules.EnsureCanDecide(Approver, recorded, true, null);

        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanDecide(ViewerOf(UserOf(2), AppPermissions.ApprovePriceReconciliation), recorded, true, null));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanDecide(ViewerOf(UserOf(2, role: "SuperAdmin", branch: null)), recorded, true, null));
    }

    [Fact]
    public void Deciding_needs_the_permission_a_recorded_result_and_a_reason_for_a_return()
    {
        var recorded = Rec(ReconciliationStatus.NeedsReview, preparedBy: 2);
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanDecide(Approver, recorded, false, " "));
        ReconciliationRules.EnsureCanDecide(Approver, recorded, false, "أكملوا المراجع");
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanDecide(Approver, Rec(ReconciliationStatus.Draft), true, null));
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanDecide(Approver, Rec(ReconciliationStatus.ApprovedAfterAudit), true, null));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanDecide(Auditor, recorded, true, null));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanDecide(ViewerOf(UserOf(9, branch: 99), AppPermissions.ApprovePriceReconciliation), recorded, true, null));
    }

    [Fact]
    public void Referring_follows_approval_once_with_a_written_recipient()
    {
        var approved = Rec(ReconciliationStatus.ApprovedAfterAudit);
        ReconciliationRules.EnsureCanRefer(Approver, approved, "المدير المالي");

        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRefer(Approver, approved, " "));
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRefer(Approver, Rec(ReconciliationStatus.PriceDifference), "س"));
        approved.ReferredAt = Today;
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanRefer(Approver, approved, "س"));
        Assert.Throws<UnauthorizedAccessException>(() => ReconciliationRules.EnsureCanRefer(Auditor, Rec(ReconciliationStatus.ApprovedAfterAudit), "س"));
    }

    [Fact]
    public void A_recorded_or_approved_reconciliation_cannot_be_edited_or_cancelled()
    {
        ReconciliationRules.EnsureCanCancel(Auditor, Rec());
        ReconciliationRules.EnsureCanCancel(Auditor, Rec(ReconciliationStatus.ReturnedForCorrection));
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanCancel(Auditor, Rec(ReconciliationStatus.Match)));
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanEdit(Auditor, Rec(ReconciliationStatus.ApprovedAfterAudit)));
        Assert.Throws<InvalidOperationException>(() => ReconciliationRules.EnsureCanEdit(Auditor, Rec(ReconciliationStatus.Cancelled)));
    }
}
