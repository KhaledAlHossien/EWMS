using System.Net;
using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// مطابقة الفواتير على القاعدة الحقيقية: ربط البنود بسعر مرجعي معتمد وتجميده، الفروق التي يحسبها الخادم، النتيجة المقترحة معلومة،
/// المدقق يسجّل والمعتمِد غيره (حتى مدير النظام لا يعتمد ما دقّقه)، الإحالة بعد الاعتماد، والمعتمد لا يُعدَّل.
/// </summary>
[Collection(ApiCollection.Name)]
public class PriceReconciliationTests(ApiFixture api)
{
    private const string Base = "/api/PriceReconciliations";

    private sealed record Org(ApiClient Auditor, ApiClient Approver, ApiClient Viewer, ApiClient Nobody, ApiClient PriceApprover);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    private async Task<ApiClient> UserAsync(string label, int roleId)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("مطابقة " + label, email, roleId, api.BranchId, api.DepartmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return api.WithToken(login.Token, id);
    }

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var auditor = await api.CreateRoleAsync("مدقق فواتير " + suffix, ["PreparePriceReconciliation", "ViewNotifications"], all);
        var approver = await api.CreateRoleAsync("معتمد مطابقات " + suffix, ["ApprovePriceReconciliation", "ViewNotifications"], all);
        var viewer = await api.CreateRoleAsync("قارئ مطابقات " + suffix, ["ViewPriceReconciliations"], all);
        var nobody = await api.CreateRoleAsync("بلا مطابقات " + suffix, ["ViewNotifications"], all);
        var priceApprover = await api.CreateRoleAsync("معتمد أسعار (مطابقة) " + suffix, ["ApproveReferencePrice", "ViewReferencePrices"], all);
        return new Org(await UserAsync("aud", auditor), await UserAsync("apr", approver), await UserAsync("vwr", viewer), await UserAsync("nob", nobody), await UserAsync("pap", priceApprover));
    }

    private async Task<ContractorResponseDto> SupplierAsync() =>
        (await api.Admin.Post<ContractorResponseDto>("/api/Contractors/Create", new { branchId = api.BranchId, name = $"مورد {Guid.NewGuid():N}"[..14], isActive = true })).Ok();

    /// <summary>سعر مرجعي معتمد للاستخدام الحالي (يضيفه مدير النظام ويعتمده غيره)</summary>
    private async Task<ReferencePriceDetailDto> ApprovedPriceAsync(Org org, ContractorResponseDto supplier, string name, decimal price, string unit = "م", string currency = "SYP")
    {
        var added = (await api.Admin.Post<ReferencePriceDetailDto>("/api/ReferencePrices/Create", new
        {
            itemName = name, unit, supplierId = supplier.Id, unitPrice = price, currency, priceDate = DateTime.Today.AddDays(-3), source = 2, sourceDocument = "عرض مرجعي"
        })).Ok();
        return (await org.PriceApprover.Post<ReferencePriceDetailDto>($"/api/ReferencePrices/Approve/{added.Id}", new { note = (string?)null, rowVersion = added.RowVersion })).Ok();
    }

    private static object Line(string description, decimal qty, decimal price, int? referenceId = null, string unit = "م") =>
        new { description, unit, quantity = qty, unitPrice = price, referenceKind = referenceId == null ? 1 : 3, referenceId };

    private object Body(ContractorResponseDto supplier, string number, object[] lines, string currency = "SYP", int kind = 1) => new
    {
        documentKind = kind, documentNumber = number, documentDate = DateTime.Today.AddDays(-1), supplierId = supplier.Id, currency, notes = "فاتورة اختبار", lines
    };

    private static string Num() => $"F-{Guid.NewGuid():N}"[..10];

    // ───────── المسار الكامل ─────────

    [Fact]
    public async Task A_document_is_compared_with_an_approved_reference_then_recorded_by_the_auditor_approved_by_someone_else_and_referred()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var price = await ApprovedPriceAsync(org, supplier, $"سلك {Guid.NewGuid():N}"[..10], 100m);

        var rec = (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [Line("سلك", 10m, 120m, price.Id)]))).Ok();
        Assert.Equal(1, rec.Status);
        Assert.StartsWith("MAT-", rec.Number);
        var line = rec.Lines.Single();
        Assert.True(line.Comparable);
        Assert.Equal(100m, line.ReferenceUnitPrice);
        Assert.Equal(20m, line.PriceDifference);                         // 120 − 100
        Assert.True(line.HasDifference);
        Assert.Equal(4, rec.SuggestedResult);                            // فرق سعر (اقتراح لا قرار)
        Assert.Equal(1200m, rec.Total);

        // «مطابق» لا يُسجَّل والمقارنة تخالفه؛ والنتيجة بلا ملاحظة مرفوضة؛ غير المدقق لا يسجّل
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Record/{rec.Id}", new { result = 2, note = (string?)null, rowVersion = rec.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Record/{rec.Id}", new { result = 4, note = " ", rowVersion = rec.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Approver.Post<ReconciliationDetailDto>($"{Base}/Record/{rec.Id}", new { result = 4, note = "فرق", rowVersion = rec.RowVersion })).Status);

        rec = (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Record/{rec.Id}", new { result = 4, note = "السعر أعلى من المرجع بـ20", rowVersion = rec.RowVersion })).Ok();
        Assert.Equal(4, rec.Status);
        Assert.Equal(4, rec.AuditResult);
        Assert.True(rec.CanDecide == false);                             // المدقق نفسه لا يرى زر الاعتماد

        // المدقق لا يعتمد (ولا مدير النظام إن كان هو المدقق)؛ والإعادة تشترط سبباً؛ غيره يعتمد
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Decide/{rec.Id}", new { approve = true, note = (string?)null, rowVersion = rec.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<ReconciliationDetailDto>($"{Base}/Decide/{rec.Id}", new { approve = false, note = " ", rowVersion = rec.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<ReconciliationDetailDto>($"{Base}/Refer/{rec.Id}", new { recipient = "المالية", note = (string?)null, rowVersion = rec.RowVersion })).Status);   // قبل الاعتماد
        rec = (await org.Approver.Post<ReconciliationDetailDto>($"{Base}/Decide/{rec.Id}", new { approve = true, note = "معتمد", rowVersion = rec.RowVersion })).Ok();
        Assert.Equal(8, rec.Status);
        Assert.Equal(4, rec.AuditResult);                                // نتيجة المدقق باقية

        // الإحالة مرة واحدة، والمعتمد لا يُعدَّل ولا يُلغى
        rec = (await org.Approver.Post<ReconciliationDetailDto>($"{Base}/Refer/{rec.Id}", new { recipient = "المدير المالي", note = "للمحاسبة", rowVersion = rec.RowVersion })).Ok();
        Assert.Equal("المدير المالي", rec.ReferredTo);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<ReconciliationDetailDto>($"{Base}/Refer/{rec.Id}", new { recipient = "غيره", note = (string?)null, rowVersion = rec.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Cancel/{rec.Id}", new { note = (string?)null, rowVersion = rec.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Put<ReconciliationDetailDto>($"{Base}/Update/{rec.Id}", Body(supplier, rec.DocumentNumber, [Line("سلك", 10m, 100m, price.Id)]))).Status);
    }

    [Fact]
    public async Task A_returned_reconciliation_is_corrected_and_recorded_again_and_the_reference_stays_frozen()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var name = $"كبل {Guid.NewGuid():N}"[..10];
        var price = await ApprovedPriceAsync(org, supplier, name, 50m);

        var rec = (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [Line(name, 4m, 50m, price.Id)]))).Ok();
        Assert.Equal(2, rec.SuggestedResult);                            // مطابق
        rec = (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Record/{rec.Id}", new { result = 2, note = (string?)null, rowVersion = rec.RowVersion })).Ok();
        rec = (await org.Approver.Post<ReconciliationDetailDto>($"{Base}/Decide/{rec.Id}", new { approve = false, note = "أضيفوا ملاحظة البند", rowVersion = rec.RowVersion })).Ok();
        Assert.Equal(9, rec.Status);
        Assert.True(rec.CanEdit);

        // تصحيح المرجع بسجل بديل لا يغيّر المرجع المجمَّد في البند
        (await org.Auditor.Put<ReconciliationDetailDto>($"{Base}/Update/{rec.Id}", new
        {
            documentKind = 1, documentNumber = rec.DocumentNumber, documentDate = DateTime.Today.AddDays(-1), supplierId = supplier.Id, currency = "SYP", notes = "بعد التصحيح",
            lines = new[] { Line(name, 5m, 50m, price.Id) }, rowVersion = rec.RowVersion
        })).Ok();
        rec = (await org.Auditor.Get<ReconciliationDetailDto>($"{Base}/Get/{rec.Id}")).Ok();
        Assert.Equal(5m, rec.Lines.Single().Quantity);
        Assert.Equal(50m, rec.Lines.Single().ReferenceUnitPrice);
        rec = (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Record/{rec.Id}", new { result = 2, note = (string?)null, rowVersion = rec.RowVersion })).Ok();
        Assert.Equal(2, rec.Status);
    }

    // ───────── المرجع والمقارنة ─────────

    [Fact]
    public async Task A_line_in_another_unit_or_currency_or_without_a_reference_is_not_comparable_and_a_bad_reference_is_refused()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var other = await SupplierAsync();
        var name = $"بلاط {Guid.NewGuid():N}"[..10];
        var meters = await ApprovedPriceAsync(org, supplier, name, 100m);
        var dollars = await ApprovedPriceAsync(org, supplier, name + " د", 2m, currency: "USD");
        var otherSupplierPrice = await ApprovedPriceAsync(org, other, name + " آخر", 10m);

        var rec = (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [
            Line(name, 1m, 100m, meters.Id, unit: "لفة"),     // وحدة مختلفة
            Line(name + " د", 1m, 2m, dollars.Id),           // عملة المرجع USD ومستند SYP
            Line("بند بلا مرجع", 1m, 5m)]))).Ok();
        Assert.All(rec.Lines, l => Assert.False(l.Comparable));
        Assert.Contains("لا تُقارَن", rec.Lines[0].NotComparableReason);
        Assert.Equal(6, rec.SuggestedResult);                            // بيانات غير مكتملة
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Record/{rec.Id}", new { result = 2, note = "x", rowVersion = rec.RowVersion })).Status);

        // مرجع لمورد آخر، أو غير معتمد، أو مجهول ← مرفوض
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [Line(name, 1m, 1m, otherSupplierPrice.Id)]))).Status);
        var historical = (await api.Admin.Post<ReferencePriceDetailDto>("/api/ReferencePrices/Create", new
        {
            itemName = name + " تاريخي", unit = "م", supplierId = supplier.Id, unitPrice = 9m, currency = "SYP", priceDate = DateTime.Today.AddDays(-2), source = 2, sourceDocument = "م"
        })).Ok();
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [Line(name, 1m, 1m, historical.Id)]))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [Line(name, 1m, 1m, 999999)]))).Status);

        // المرشَّحون: السعر المعتمد لهذا المورد فقط
        var candidates = (await org.Auditor.Get<List<ReconciliationCandidateDto>>($"{Base}/Candidates?supplierId={supplier.Id}")).Ok();
        Assert.Contains(candidates, c => c.Kind == 3 && c.Id == meters.Id);
        Assert.DoesNotContain(candidates, c => c.Id == historical.Id || c.Id == otherSupplierPrice.Id);
    }

    // ───────── الصلاحية والتكرار والتقرير ─────────

    [Fact]
    public async Task Permissions_duplicates_and_the_report()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var number = Num();
        var rec = (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, number, [Line("بند", 1m, 5m)]))).Ok();

        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, number, [Line("بند", 1m, 5m)]))).Status);      // المستند نفسه مرتين
        (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, number, [Line("بند", 1m, 5m)], kind: 2))).Ok();                                          // كشف بالرقم نفسه مختلف
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Viewer.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [Line("بند", 1m, 5m)]))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Nobody.Get<PagedResultDto<ReconciliationListItemDto>>($"{Base}/Search")).Status);
        Assert.Equal(HttpStatusCode.OK, (await org.Viewer.Get<ReconciliationDetailDto>($"{Base}/Get/{rec.Id}")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Auditor.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [Line("بند", 1m, 5m)], currency: "syp"))).Status);

        var delete = await api.Admin.Delete($"/api/Contractors/Delete/{supplier.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.Status);          // مورد له مطابقة لا يُحذف

        var report = await api.Admin.Post<JsonElement>("/api/Reports/Preview/price-reconciliations", new { filters = new Dictionary<string, string>() });
        Assert.Equal(HttpStatusCode.OK, report.Status);
        Assert.Contains(number, report.Raw);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Nobody.Post<JsonElement>("/api/Reports/Preview/price-reconciliations", new { filters = new Dictionary<string, string>() })).Status);
    }

    [Fact]
    public async Task The_super_admin_who_audited_a_document_cannot_approve_it()
    {
        var supplier = await SupplierAsync();
        var rec = (await api.Admin.Post<ReconciliationDetailDto>($"{Base}/Create", Body(supplier, Num(), [Line("بند", 1m, 5m)]))).Ok();
        rec = (await api.Admin.Post<ReconciliationDetailDto>($"{Base}/Record/{rec.Id}", new { result = 6, note = "لا مرجع", rowVersion = rec.RowVersion })).Ok();
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Admin.Post<ReconciliationDetailDto>($"{Base}/Decide/{rec.Id}", new { approve = true, note = (string?)null, rowVersion = rec.RowVersion })).Status);
    }
}
