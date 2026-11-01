using System.Net;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using ClosedXML.Excel;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// استيراد الأسعار المرجعية من Excel على القاعدة الحقيقية: المعاينة لا تحفظ، الكل أو لا شيء (سطر خاطئ واحد يمنع الحفظ كله)،
/// المورد من السجل، التكرار خطأ، والأسعار المستوردة تاريخية بانتظار اعتماد غير المستورِد وتشير إلى دفعتها.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReferencePriceImportTests(ApiFixture api)
{
    private const string Base = "/api/ReferencePrices";

    private sealed record Org(ApiClient Importer, ApiClient Approver, ApiClient Adder, ApiClient Viewer);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    private async Task<ApiClient> UserAsync(string label, int roleId)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("استيراد " + label, email, roleId, api.BranchId, api.DepartmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return api.WithToken(login.Token, id);
    }

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var importerRole = await api.CreateRoleAsync("مستورد أسعار " + suffix, ["ImportReferencePrices", "ViewNotifications"], all);
        var approverRole = await api.CreateRoleAsync("معتمد أسعار مستوردة " + suffix, ["ApproveReferencePrice", "ViewReferencePrices", "ViewNotifications"], all);
        var adderRole = await api.CreateRoleAsync("مدخل أسعار (استيراد) " + suffix, ["AddReferencePrice"], all);
        var viewerRole = await api.CreateRoleAsync("قارئ أسعار (استيراد) " + suffix, ["ViewReferencePrices"], all);
        return new Org(await UserAsync("imp", importerRole), await UserAsync("apr", approverRole), await UserAsync("add", adderRole), await UserAsync("vwr", viewerRole));
    }

    private async Task<ContractorResponseDto> SupplierAsync(bool active = true)
    {
        var supplier = (await api.Admin.Post<ContractorResponseDto>("/api/Contractors/Create", new { branchId = api.BranchId, name = $"مورد {Guid.NewGuid():N}"[..14], isActive = true })).Ok();
        if (!active) (await api.Admin.Put<ContractorResponseDto>($"/api/Contractors/Update/{supplier.Id}", new { branchId = api.BranchId, name = supplier.Name, isActive = false, rowVersion = supplier.RowVersion })).Ok();
        return supplier;
    }

    private static readonly string[] Headers = ["رمز المادة", "اسم المادة", "المواصفات", "الوحدة", "المورد", "سعر الوحدة", "العملة", "تاريخ السعر", "مصدر السعر", "ملاحظات"];

    private static byte[] Xlsx(params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("الأسعار");
        for (var c = 0; c < Headers.Length; c++) sheet.Cell(1, c + 1).Value = Headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++) sheet.Cell(r + 2, c + 1).Value = rows[r][c];
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string[] Line(string name, string supplier, string price, string date, string unit = "م", string currency = "SYP", string source = "عرض 7", string code = "") =>
        [code, name, "مجدول", unit, supplier, price, currency, date, source, ""];

    private static string Day(int ago) => DateTime.Today.AddDays(-ago).ToString("yyyy-MM-dd");

    // ───────── المعاينة والحفظ ─────────

    [Fact]
    public async Task A_dry_run_saves_nothing_and_a_confirmed_import_saves_every_row_as_a_historical_price_pointing_to_its_batch()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var name = $"إسمنت {Guid.NewGuid():N}"[..12];
        var file = Xlsx(Line(name, supplier.Name, "100.5", Day(5)), Line(name, supplier.Name, "90", Day(40)), Line(name, supplier.Name, "2.5", Day(10), currency: "USD"));

        var preview = (await org.Importer.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=true", "prices.xlsx", file)).Ok();
        Assert.True(preview.DryRun);
        Assert.False(preview.Saved);
        Assert.Equal(3, preview.Total);
        Assert.Equal(3, preview.Valid);
        Assert.All(preview.Rows, r => Assert.True(r.Valid));
        Assert.Equal(0, (await org.Viewer.Get<PagedResultDto<ReferencePriceListItemDto>>($"{Base}/Search?search={Uri.EscapeDataString(name)}")).Ok().TotalCount);   // لم يُحفظ شيء

        var saved = (await org.Importer.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=false", "prices.xlsx", file)).Ok();
        Assert.True(saved.Saved);
        Assert.Equal(3, saved.Imported);
        Assert.NotNull(saved.ImportId);

        var list = (await org.Viewer.Get<PagedResultDto<ReferencePriceListItemDto>>($"{Base}/Search?search={Uri.EscapeDataString(name)}")).Ok();
        Assert.Equal(3, list.TotalCount);
        Assert.All(list.Items, p => { Assert.Equal(1, p.Status); Assert.Equal(3, p.Source); Assert.Equal("عرض 7", p.SourceDocument); });
        Assert.Single(list.Items, p => p.IsLatest && p.Currency == "SYP");                  // آخر سعر بالليرة هو الأحدث تاريخاً
        var detail = (await org.Viewer.Get<ReferencePriceDetailDto>($"{Base}/Get/{list.Items[0].Id}")).Ok();
        Assert.Equal(saved.ImportId, detail.ImportId);
        Assert.Equal("prices.xlsx", detail.ImportFileName);

        // المستورِد لا يعتمد ما استورده؛ غيره يعتمد
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Importer.Post<ReferencePriceDetailDto>($"{Base}/Approve/{detail.Id}", new { note = (string?)null, rowVersion = detail.RowVersion })).Status);
        Assert.Equal(2, (await org.Approver.Post<ReferencePriceDetailDto>($"{Base}/Approve/{detail.Id}", new { note = (string?)null, rowVersion = detail.RowVersion })).Ok().Status);
    }

    [Fact]
    public async Task One_bad_row_saves_nothing_at_all_and_every_problem_is_reported_by_row()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var inactive = await SupplierAsync(active: false);
        var name = $"حديد {Guid.NewGuid():N}"[..12];
        var file = Xlsx(
            Line(name, supplier.Name, "100", Day(3)),                         // سليم
            Line(name, "مورد لا وجود له", "100", Day(4)),                     // مورد مجهول
            Line(name, inactive.Name, "100", Day(5)),                         // مورد معطَّل
            Line(name, supplier.Name, "1,250", Day(6)),                       // فاصلة
            Line(name, supplier.Name, "100", "05/10/2026"),                   // تاريخ ملتبس
            Line(name, supplier.Name, "100", Day(7), source: " "),            // بلا مصدر
            Line(name, supplier.Name, "100", Day(8), currency: "syp1"),       // عملة خاطئة
            Line(name, supplier.Name, "100", Day(3)));                        // مكرر للسطر الأول

        var report = (await org.Importer.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=false", "bad.xlsx", file)).Ok();

        Assert.False(report.Saved);
        Assert.Equal(0, report.Imported);
        Assert.Equal(8, report.Total);
        Assert.Equal(1, report.Valid);
        Assert.Equal(7, report.Invalid);
        Assert.True(report.Rows[0].Valid);                                    // السطر السليم سليم لكنه لم يُحفظ
        Assert.Contains(report.Rows[1].Errors, e => e.Contains("غير موجود في سجل الفرع"));
        Assert.Contains(report.Rows[2].Errors, e => e.Contains("معطَّل"));
        Assert.Contains(report.Rows[3].Errors, e => e.Contains("نقطة عشرية"));
        Assert.Contains(report.Rows[4].Errors, e => e.Contains("2026-10-05"));
        Assert.Contains(report.Rows[5].Errors, e => e.Contains("مصدر"));
        Assert.Contains(report.Rows[7].Errors, e => e.Contains("مكرر في الملف"));
        Assert.Equal(0, (await org.Viewer.Get<PagedResultDto<ReferencePriceListItemDto>>($"{Base}/Search?search={Uri.EscapeDataString(name)}")).Ok().TotalCount);
    }

    [Fact]
    public async Task A_price_already_recorded_makes_the_row_invalid_and_a_fixed_file_then_imports()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var name = $"بلاط {Guid.NewGuid():N}"[..12];
        (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Create", new
        {
            itemName = name, unit = "م", supplierId = supplier.Id, unitPrice = 75m, currency = "SYP", priceDate = DateTime.Today.AddDays(-9), source = 2, sourceDocument = "عرض يدوي"
        })).Ok();

        var duplicate = (await org.Importer.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=true", "dup.xlsx", Xlsx(Line(name, supplier.Name, "75", Day(9))))).Ok();
        Assert.Equal(1, duplicate.Invalid);
        Assert.Contains(duplicate.Rows[0].Errors, e => e.Contains("مسجَّل مسبقاً"));

        var fixedFile = (await org.Importer.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=false", "ok.xlsx", Xlsx(Line(name, supplier.Name, "80", Day(9))))).Ok();
        Assert.True(fixedFile.Saved);
    }

    // ───────── الصلاحية والقالب والملف ─────────

    [Fact]
    public async Task Only_the_import_permission_opens_the_import_and_the_template_downloads()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var file = Xlsx(Line($"مادة {Guid.NewGuid():N}"[..12], supplier.Name, "10", Day(1)));

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Adder.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=true", "x.xlsx", file)).Status);   // الإضافة اليدوية لا تكفي
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Viewer.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=true", "x.xlsx", file)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Adder.Get<object>($"{Base}/ImportTemplate")).Status);

        Assert.Equal(HttpStatusCode.OK, (await org.Importer.Probe(HttpMethod.Get, $"{Base}/ImportTemplate")).Status);
    }

    [Fact]
    public async Task A_file_that_is_not_excel_or_lacks_required_columns_is_refused_with_a_clear_message()
    {
        var org = await OrgAsync();
        var junk = await org.Importer.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=true", "x.xlsx", "ليس إكسل"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, junk.Status);

        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("الأسعار").Cell(1, 1).Value = "اسم المادة";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var missing = await org.Importer.PostFile<ReferencePriceImportReportDto>($"{Base}/Import?dryRun=true", "x.xlsx", stream.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, missing.Status);
    }
}
