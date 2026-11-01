using Application.Common;
using Application.Features.ReferencePrices;
using Application.Interfaces;
using Domain.Entities;

namespace Projects.Tests;

/// <summary>
/// استيراد الأسعار المرجعية من Excel: فحص كل سطر بقواعد الإضافة نفسها، المورد باسمه من السجل (نشط)، أرقام وتواريخ بصيغ صريحة،
/// تكرار داخل الملف ومع المسجَّل خطأ، والصلاحية والفرع.
/// </summary>
public class ReferencePriceImportRulesTests
{
    private const int Branch = 1;
    private static readonly DateTime Today = new(2026, 11, 1);

    private static Dictionary<string, Contractor> Suppliers() => new()
    {
        [ReferencePriceImportRules.NormName("مورد أ")] = new Contractor { Id = 7, Name = "مورد أ", BranchId = Branch, IsActive = true },
        [ReferencePriceImportRules.NormName("مورد معطل")] = new Contractor { Id = 8, Name = "مورد معطل", BranchId = Branch, IsActive = false }
    };

    private static ReferencePriceSheetRow Row(int n = 2, string name = "سلك نحاس", string unit = "م", string supplier = "مورد أ", string price = "100.5", string currency = "SYP",
        string date = "2026-10-05", string source = "عرض 7", string code = "", string notes = "") => new()
    {
        Row = n, ItemCode = code, ItemName = name, Unit = unit, Supplier = supplier, UnitPrice = price, Currency = currency, PriceDate = date, SourceDocument = source, Notes = notes
    };

    private static ReferencePriceImportRules.RowResult Check(ReferencePriceSheetRow row) => ReferencePriceImportRules.ValidateRow(row, Suppliers(), Today);

    private static User UserOf(int id, string role = "Emp", int? branch = Branch) => new()
    {
        Id = id, FullName = "u" + id, Email = $"u{id}@x.sy", PasswordHash = "x", RoleId = 1, IsActive = true,
        Role = new Role { Id = 1, Name = role }, BranchId = branch, DepartmentId = 10
    };

    // ───────── الأرقام والتواريخ ─────────

    [Theory]
    [InlineData("100", 100)]
    [InlineData("1250.5", 1250.5)]
    [InlineData(" 7.25 ", 7.25)]
    [InlineData("١٢٣٫٥", 123.5)]            // أرقام عربية وفاصلة عشرية عربية
    [InlineData("0", 0)]
    public void A_price_is_read_with_a_decimal_point(string text, double expected)
    {
        Assert.True(ReferencePriceImportRules.TryParsePrice(text, out var price, out _));
        Assert.Equal((decimal)expected, price);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,250")]                    // فاصلة آلاف أو عشرية: قراءتان فتُرفض
    [InlineData("12,5")]
    [InlineData("1.2.3")]
    public void An_ambiguous_or_invalid_price_is_rejected(string text)
    {
        Assert.False(ReferencePriceImportRules.TryParsePrice(text, out _, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Theory]
    [InlineData("2026-10-05")]
    [InlineData("2026/10/05")]
    [InlineData("2026-10-05 00:00:00")]
    [InlineData("٢٠٢٦-١٠-٠٥")]
    public void A_date_is_read_in_the_explicit_year_first_format(string text)
    {
        Assert.True(ReferencePriceImportRules.TryParseDate(text, out var date));
        Assert.Equal(new DateTime(2026, 10, 5), date);
    }

    [Theory]
    [InlineData("05/10/2026")]               // يوم/شهر ملتبس
    [InlineData("10/05/2026")]
    [InlineData("2026-13-01")]
    [InlineData("")]
    [InlineData("غداً")]
    public void An_ambiguous_date_is_rejected_rather_than_guessed(string text) => Assert.False(ReferencePriceImportRules.TryParseDate(text, out _));

    // ───────── فحص السطر ─────────

    [Fact]
    public void A_complete_row_parses_with_a_normalised_currency_and_the_supplier_id()
    {
        var result = Check(Row(currency: " syp ", code: " CU-25 "));
        Assert.True(result.Valid);
        Assert.Equal(7, result.Price!.SupplierId);
        Assert.Equal("SYP", result.Price.Currency);
        Assert.Equal("CU-25", result.Price.ItemCode);
        Assert.Equal(100.5m, result.Price.UnitPrice);
        Assert.Equal(new DateTime(2026, 10, 5), result.Price.PriceDate);
    }

    [Fact]
    public void The_supplier_is_matched_by_name_ignoring_case_and_spaces_but_never_created()
    {
        Assert.True(Check(Row(supplier: "  مورد   أ ")).Valid);
        var unknown = Check(Row(supplier: "مورد غير موجود"));
        Assert.False(unknown.Valid);
        Assert.Contains("غير موجود في سجل الفرع", unknown.Errors.Single());
        Assert.Contains("معطَّل", Check(Row(supplier: "مورد معطل")).Errors.Single());
        Assert.Contains("المورد مطلوب", Check(Row(supplier: " ")).Errors);
    }

    [Fact]
    public void A_row_needs_the_same_things_as_a_manual_price_name_unit_currency_date_and_a_source()
    {
        Assert.False(Check(Row(name: " ")).Valid);
        Assert.False(Check(Row(unit: "")).Valid);
        Assert.False(Check(Row(currency: "SY")).Valid);
        Assert.False(Check(Row(source: " ")).Valid);                         // لا سعر بلا مصدر
        Assert.False(Check(Row(date: "2026-11-02")).Valid);                  // مستقبل
        Assert.False(Check(Row(price: "-5")).Valid);
        Assert.False(Check(Row(price: "1,5")).Valid);
    }

    [Fact]
    public void Every_problem_of_a_bad_price_or_date_is_reported_together_with_a_bad_supplier()
    {
        var result = Check(Row(price: "abc", date: "05/10/2026", supplier: "نكرة"));
        Assert.Equal(3, result.Errors.Count);
        Assert.Null(result.Price);
    }

    // ───────── التكرار ─────────

    [Fact]
    public void The_same_price_twice_in_the_file_flags_the_later_row_only()
    {
        var results = new[] { Check(Row(2)), Check(Row(3, price: "101")), Check(Row(4)) };
        ReferencePriceImportRules.FlagFileDuplicates(results);

        Assert.True(results[0].Valid);
        Assert.True(results[1].Valid);                                       // سعر مختلف: ليس تكراراً
        Assert.False(results[2].Valid);
        Assert.Contains("السطر 2", results[2].Errors.Single());
    }

    [Fact]
    public void A_price_already_recorded_is_flagged_but_a_different_unit_currency_or_date_is_not()
    {
        var existing = new ReferencePrice
        {
            Id = 41, BranchId = Branch, ItemName = "سلك نحاس", Unit = "م", Currency = "SYP", SupplierId = 7, UnitPrice = 100.5m, PriceDate = new DateTime(2026, 10, 5), CreatedAt = new DateTime(2026, 10, 6)
        };
        var results = new[] { Check(Row(2)), Check(Row(3, unit: "لفة")), Check(Row(4, currency: "USD")), Check(Row(5, date: "2026-10-06")), Check(Row(6, name: " سلك  نحاس ")) };
        ReferencePriceImportRules.FlagExistingDuplicates(results, [existing]);

        Assert.False(results[0].Valid);
        Assert.Contains("REF-2026-00041", results[0].Errors.Single());
        Assert.True(results[1].Valid);
        Assert.True(results[2].Valid);
        Assert.True(results[3].Valid);
        Assert.False(results[4].Valid);                                      // مسافات زائدة لا تُغيّر المادة
    }

    // ───────── الصلاحية ─────────

    [Fact]
    public void Importing_needs_its_own_permission_and_the_branch()
    {
        var importer = new Viewer(UserOf(2), new HashSet<string> { AppPermissions.ImportReferencePrices });
        ReferencePriceImportRules.EnsureCanImport(importer, Branch);

        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceImportRules.EnsureCanImport(new Viewer(UserOf(3), new HashSet<string> { AppPermissions.AddReferencePrice }), Branch));
        Assert.Throws<UnauthorizedAccessException>(() => ReferencePriceImportRules.EnsureCanImport(importer, 99));
        ReferencePriceImportRules.EnsureCanImport(new Viewer(UserOf(4, role: "SuperAdmin", branch: null), new HashSet<string>()), 99);
    }
}
