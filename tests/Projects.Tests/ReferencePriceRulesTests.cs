using Application.Common;
using Application.Features.ReferencePrices;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>
/// قواعد الأسعار المرجعية: سجل لا يُعدَّل (التصحيح بسجل بديل)، الإضافة غير الاعتماد (من أضاف لا يعتمد حتى مدير النظام)،
/// لا سعر بلا مصدر وتاريخ، «آخر سعر» الأحدث تاريخاً لمادة بوحدة وعملة واحدة (لا الأعلى ولا الأقل)، وحدّ الفرع.
/// </summary>
public class ReferencePriceRulesTests
{
    private const int Branch = 1;
    private static readonly DateTime Today = new(2026, 11, 1);

    private static User UserOf(int id, string role = "Emp", int? branch = Branch) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1, IsActive = true,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = 10
    };

    private static Viewer ViewerOf(User user, params string[] permissions) => new(user, new HashSet<string>(permissions));

    private static ReferencePrice PriceOf(int id = 1, string? code = null, string name = "سلك نحاس", string unit = "م", string currency = "SYP", int supplier = 7,
        DateTime? date = null, ReferencePriceStatus status = ReferencePriceStatus.Historical, int createdBy = 2, int branch = Branch) => new()
    {
        Id = id, BranchId = branch, ItemCode = code, ItemName = name, Unit = unit, Currency = currency, SupplierId = supplier, UnitPrice = 100m,
        PriceDate = date ?? Today.AddDays(-10), Status = status, CreatedByUserId = createdBy, SourceDocument = "عرض 5"
    };

    private static Contractor SupplierOf(int id = 7, int branch = Branch, bool active = true) => new() { Id = id, BranchId = branch, IsActive = active };

    private static string? Validate(string? code = null, string? name = "سلك", string? unit = "م", decimal price = 10m, string? currency = "SYP", DateTime? date = null,
        int source = 1, string? sourceDocument = "عرض 5", string? notes = null, bool allowImport = false) =>
        ReferencePriceRules.ValidateInput(code, name, null, unit, price, currency, date ?? Today.AddDays(-1), source, sourceDocument, notes, Today, allowImport);

    // ───────── الأسماء والمدخلات ─────────

    [Fact]
    public void The_internal_number_follows_the_creation_year_and_pads_the_id()
    {
        Assert.Equal("REF-2026-00042", ReferencePriceRules.Number(42, new DateTime(2026, 3, 1)));
    }

    [Fact]
    public void Input_needs_a_name_a_unit_a_valid_currency_and_a_source()
    {
        Assert.Null(Validate());
        Assert.NotNull(Validate(name: " "));
        Assert.NotNull(Validate(unit: ""));
        Assert.NotNull(Validate(currency: "syp"));
        Assert.NotNull(Validate(currency: "SY"));
        Assert.NotNull(Validate(price: -1m));
        Assert.NotNull(Validate(sourceDocument: " "));          // لا سعر بلا مصدر
        Assert.NotNull(Validate(code: new string('a', 51)));
    }

    [Fact]
    public void A_price_date_is_not_in_the_future_and_not_ancient()
    {
        Assert.Null(Validate(date: Today));
        Assert.NotNull(Validate(date: Today.AddDays(1)));
        Assert.NotNull(Validate(date: Today.AddYears(-21)));
    }

    [Fact]
    public void The_import_source_cannot_be_claimed_by_the_add_form()
    {
        Assert.NotNull(Validate(source: 3));
        Assert.Null(Validate(source: 3, allowImport: true));
        Assert.NotNull(Validate(source: 9, allowImport: true));
    }

    // ───────── الرؤية والإضافة ─────────

    [Fact]
    public void A_price_is_seen_by_holders_of_any_reference_permission_within_the_branch_only()
    {
        var price = PriceOf();
        ReferencePriceRules.EnsureCanView(ViewerOf(UserOf(20), AppPermissions.ViewReferencePrices), price);
        ReferencePriceRules.EnsureCanView(ViewerOf(UserOf(21), AppPermissions.AddReferencePrice), price);
        ReferencePriceRules.EnsureCanView(ViewerOf(UserOf(22), AppPermissions.ApproveReferencePrice), price);

        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanView(ViewerOf(UserOf(23)), price));                                          // بلا صلاحية
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanView(ViewerOf(UserOf(24, branch: 99), AppPermissions.ViewReferencePrices), price)); // فرع آخر
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanView(ViewerOf(UserOf(25, branch: null), AppPermissions.ViewReferencePrices), price)); // بلا فرع لا يطابق (null != فرع)
    }

    [Fact]
    public void Adding_needs_the_permission_the_branch_and_a_usable_supplier()
    {
        var adder = ViewerOf(UserOf(2), AppPermissions.AddReferencePrice);
        ReferencePriceRules.EnsureCanAdd(adder, Branch, SupplierOf(), null);

        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanAdd(ViewerOf(UserOf(3), AppPermissions.ViewReferencePrices), Branch, SupplierOf(), null));
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanAdd(adder, 99, SupplierOf(branch: 99), null));
        Assert.Throws<InvalidOperationException>(() => ReferencePriceRules.EnsureCanAdd(adder, Branch, SupplierOf(active: false), null));
        Assert.Throws<InvalidOperationException>(() => ReferencePriceRules.EnsureCanAdd(adder, Branch, SupplierOf(branch: 99), null));
    }

    // ───────── التصحيح ─────────

    [Fact]
    public void Correcting_needs_the_add_permission_a_reason_and_a_live_record()
    {
        var adder = ViewerOf(UserOf(2), AppPermissions.AddReferencePrice);
        ReferencePriceRules.EnsureCanCorrect(adder, PriceOf(), "خطأ إدخال");
        ReferencePriceRules.EnsureCanCorrect(adder, PriceOf(status: ReferencePriceStatus.ApprovedForCurrentUse), "تغيّر السعر");

        Assert.Throws<InvalidOperationException>(() => ReferencePriceRules.EnsureCanCorrect(adder, PriceOf(), " "));
        Assert.Throws<InvalidOperationException>(() => ReferencePriceRules.EnsureCanCorrect(adder, PriceOf(status: ReferencePriceStatus.Superseded), "س"));
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanCorrect(ViewerOf(UserOf(3), AppPermissions.ApproveReferencePrice), PriceOf(), "س"));
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanCorrect(ViewerOf(UserOf(4, branch: 99), AppPermissions.AddReferencePrice), PriceOf(), "س"));
    }

    [Fact]
    public void A_historical_price_is_superseded_at_once_but_an_approved_one_stays_until_its_replacement_is_approved()
    {
        Assert.Equal(ReferencePriceStatus.Superseded, ReferencePriceRules.StatusAfterCorrection(ReferencePriceStatus.Historical));
        Assert.Equal(ReferencePriceStatus.ApprovedForCurrentUse, ReferencePriceRules.StatusAfterCorrection(ReferencePriceStatus.ApprovedForCurrentUse));
        Assert.Equal(ReferencePriceStatus.Superseded, ReferencePriceRules.StatusAfterCorrection(ReferencePriceStatus.Superseded));
    }

    // ───────── الاعتماد ─────────

    [Fact]
    public void Whoever_added_the_price_does_not_approve_it_even_the_super_admin()
    {
        var price = PriceOf(createdBy: 2);
        ReferencePriceRules.EnsureCanApprove(ViewerOf(UserOf(9), AppPermissions.ApproveReferencePrice), price, null);

        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanApprove(ViewerOf(UserOf(2), AppPermissions.ApproveReferencePrice), price, null));
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanApprove(ViewerOf(UserOf(2, role: "SuperAdmin", branch: null)), price, null));
    }

    [Fact]
    public void Approving_needs_the_permission_the_branch_and_a_historical_record()
    {
        var approver = ViewerOf(UserOf(9), AppPermissions.ApproveReferencePrice);
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanApprove(ViewerOf(UserOf(9), AppPermissions.AddReferencePrice), PriceOf(), null));
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceRules.EnsureCanApprove(approver, PriceOf(branch: 99), null));
        Assert.Throws<InvalidOperationException>(() => ReferencePriceRules.EnsureCanApprove(approver, PriceOf(status: ReferencePriceStatus.ApprovedForCurrentUse), null));
        Assert.Throws<InvalidOperationException>(() => ReferencePriceRules.EnsureCanApprove(approver, PriceOf(status: ReferencePriceStatus.Superseded), null));
        Assert.Throws<InvalidOperationException>(() => ReferencePriceRules.EnsureCanApprove(approver, PriceOf(), new string('x', 1001)));
    }

    // ───────── المفتاح و«آخر سعر» ─────────

    [Fact]
    public void The_key_uses_the_code_when_present_else_the_name_and_always_the_unit_and_currency()
    {
        Assert.Equal(ReferencePriceRules.Key("CU-25", "سلك أ", "م", "SYP"), ReferencePriceRules.Key(" cu-25 ", "اسم آخر", " م ", "syp"));   // الرمز يغلب الاسم، والمسافات والحالة لا تفرّق
        Assert.Equal(ReferencePriceRules.Key(null, "سلك  نحاس", "م", "SYP"), ReferencePriceRules.Key("", " سلك نحاس ", "م", "SYP"));
        Assert.NotEqual(ReferencePriceRules.Key(null, "سلك", "م", "SYP"), ReferencePriceRules.Key(null, "سلك", "لفة", "SYP"));            // وحدة أخرى
        Assert.NotEqual(ReferencePriceRules.Key(null, "سلك", "م", "SYP"), ReferencePriceRules.Key(null, "سلك", "م", "USD"));            // عملة أخرى
    }

    [Fact]
    public void The_latest_price_is_the_most_recent_by_date_not_the_highest_or_the_lowest()
    {
        var old = PriceOf(1, date: Today.AddDays(-30));
        old.UnitPrice = 50m;                              // الأرخص لكنه أقدم
        var recent = PriceOf(2, date: Today.AddDays(-5));
        recent.UnitPrice = 90m;
        var middle = PriceOf(3, date: Today.AddDays(-15));
        middle.UnitPrice = 200m;                          // الأغلى لكنه ليس الأحدث

        var (latest, _) = ReferencePriceRules.Latest([old, recent, middle]);

        Assert.Equal([2], latest.ToArray());
    }

    [Fact]
    public void The_latest_is_computed_per_unit_and_currency_and_per_supplier_and_skips_superseded()
    {
        var a = PriceOf(1, supplier: 7, date: Today.AddDays(-10));
        var b = PriceOf(2, supplier: 8, date: Today.AddDays(-4));
        var usd = PriceOf(3, currency: "USD", date: Today.AddDays(-20));
        var perMeterRoll = PriceOf(4, unit: "لفة", date: Today.AddDays(-30));
        var superseded = PriceOf(5, supplier: 7, date: Today.AddDays(-1), status: ReferencePriceStatus.Superseded);   // الأحدث لكنه مُستبدَل

        var (latest, perSupplier) = ReferencePriceRules.Latest([a, b, usd, perMeterRoll, superseded]);

        Assert.Equal(new[] { 2, 3, 4 }, latest.OrderBy(x => x));            // كل مجموعة بوحدتها وعملتها
        Assert.Equal(new[] { 1, 2, 3, 4 }, perSupplier.OrderBy(x => x));    // الأحدث لكل مورد (المُستبدَل لا يُحسب)
    }

    [Fact]
    public void A_tie_on_date_goes_to_the_later_record()
    {
        var first = PriceOf(1, date: Today.AddDays(-3));
        var second = PriceOf(2, date: Today.AddDays(-3));
        Assert.Equal([2], ReferencePriceRules.Latest([first, second]).Latest.ToArray());
    }
}
