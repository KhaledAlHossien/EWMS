using System.Net;
using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// الأسعار المرجعية على القاعدة الحقيقية: الإضافة بمصدر وتاريخ، «آخر سعر» الأحدث تاريخاً لا الأرخص ولا الأغلى، فصل الوحدات والعملات،
/// التصحيح بسجل بديل يبقى فيه القديم، الاعتماد لغير من أضاف، وحماية المورد من الحذف.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReferencePriceTests(ApiFixture api)
{
    private const string Base = "/api/ReferencePrices";

    private sealed record Org(ApiClient Adder, int AdderId, ApiClient Approver, int ApproverId, ApiClient Viewer, ApiClient Nobody);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    private async Task<(ApiClient Client, int Id)> UserAsync(string label, int roleId)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("أسعار " + label, email, roleId, api.BranchId, api.DepartmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return (api.WithToken(login.Token, id), id);
    }

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var adderRole = await api.CreateRoleAsync("مدخل أسعار " + suffix, ["AddReferencePrice", "ViewContractors", "ViewNotifications"], all);
        var approverRole = await api.CreateRoleAsync("معتمد أسعار " + suffix, ["ApproveReferencePrice", "ViewReferencePrices", "ViewNotifications"], all);
        var viewerRole = await api.CreateRoleAsync("قارئ أسعار " + suffix, ["ViewReferencePrices"], all);
        var nobodyRole = await api.CreateRoleAsync("بلا أسعار " + suffix, ["ViewNotifications"], all);

        var adder = await UserAsync("add", adderRole);
        var approver = await UserAsync("apr", approverRole);
        var viewer = await UserAsync("vwr", viewerRole);
        var nobody = await UserAsync("nob", nobodyRole);
        return new Org(adder.Client, adder.Id, approver.Client, approver.Id, viewer.Client, nobody.Client);
    }

    private async Task<ContractorResponseDto> SupplierAsync() =>
        (await api.Admin.Post<ContractorResponseDto>("/api/Contractors/Create", new { branchId = api.BranchId, name = $"مورد {Guid.NewGuid():N}"[..14], isActive = true })).Ok();

    private static object Body(ContractorResponseDto supplier, string name, decimal price, DateTime date, string unit = "م", string currency = "SYP", string? code = null, string source = "عرض رقم 7") => new
    {
        itemCode = code, itemName = name, specifications = "مجدول", unit, supplierId = supplier.Id, unitPrice = price, currency, priceDate = date, source = 2, sourceDocument = source
    };

    private async Task<ReferencePriceDetailDto> AddAsync(Org org, ContractorResponseDto supplier, string name, decimal price, DateTime date, string unit = "م", string currency = "SYP", string? code = null) =>
        (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Create", Body(supplier, name, price, date, unit, currency, code))).Ok();

    // ───────── الإضافة والاعتماد ─────────

    [Fact]
    public async Task A_price_is_added_as_historical_with_its_source_then_approved_by_someone_else()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var name = $"سلك {Guid.NewGuid():N}"[..12];

        var price = await AddAsync(org, supplier, name, 100m, DateTime.Today.AddDays(-3));
        Assert.Equal(1, price.Status);
        Assert.StartsWith("REF-", price.Number);
        Assert.Equal("عرض رقم 7", price.SourceDocument);
        Assert.True(price.IsLatest);
        Assert.False(price.CanApprove);                                        // من أضافه لا يرى زر الاعتماد

        // لا مصدر ← 400؛ بلا صلاحية الإضافة ← 403
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Create", Body(supplier, name, 10m, DateTime.Today, source: " "))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Approver.Post<ReferencePriceDetailDto>($"{Base}/Create", Body(supplier, name, 10m, DateTime.Today))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Create", Body(supplier, name, 10m, DateTime.Today.AddDays(2)))).Status);   // مستقبل

        // من أضاف لا يعتمد؛ غيره يعتمد
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Approve/{price.Id}", new { note = (string?)null, rowVersion = price.RowVersion })).Status);
        var approved = (await org.Approver.Post<ReferencePriceDetailDto>($"{Base}/Approve/{price.Id}", new { note = "سعر السوق", rowVersion = price.RowVersion })).Ok();
        Assert.Equal(2, approved.Status);
        Assert.Equal("سعر السوق", approved.ApprovalNote);

        // لا يُعتمد مرتين
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<ReferencePriceDetailDto>($"{Base}/Approve/{price.Id}", new { note = (string?)null, rowVersion = approved.RowVersion })).Status);
    }

    [Fact]
    public async Task The_super_admin_who_added_a_price_cannot_approve_it()
    {
        var supplier = await SupplierAsync();
        var price = (await api.Admin.Post<ReferencePriceDetailDto>($"{Base}/Create", Body(supplier, $"مادة {Guid.NewGuid():N}"[..12], 5m, DateTime.Today))).Ok();
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Admin.Post<ReferencePriceDetailDto>($"{Base}/Approve/{price.Id}", new { note = (string?)null, rowVersion = price.RowVersion })).Status);
    }

    // ───────── «آخر سعر» ─────────

    [Fact]
    public async Task The_latest_price_is_the_most_recent_by_date_per_unit_and_currency_never_the_cheapest_or_the_dearest()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var name = $"كبل {Guid.NewGuid():N}"[..12];

        var cheapOld = await AddAsync(org, supplier, name, 50m, DateTime.Today.AddDays(-40));
        var dearMiddle = await AddAsync(org, supplier, name, 300m, DateTime.Today.AddDays(-20));
        var recent = await AddAsync(org, supplier, name, 120m, DateTime.Today.AddDays(-2));
        var dollars = await AddAsync(org, supplier, name, 2m, DateTime.Today.AddDays(-60), currency: "USD");          // عملة أخرى: مجموعة أخرى
        var roll = await AddAsync(org, supplier, name, 9m, DateTime.Today.AddDays(-50), unit: "لفة");                  // وحدة أخرى: مجموعة أخرى

        var list = (await org.Viewer.Get<PagedResultDto<ReferencePriceListItemDto>>($"{Base}/Search?search={Uri.EscapeDataString(name)}")).Ok();
        Assert.Equal(5, list.TotalCount);
        var latest = list.Items.Where(i => i.IsLatest).Select(i => i.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { recent.Id, dollars.Id, roll.Id }.OrderBy(x => x).ToArray(), latest);
        Assert.DoesNotContain(list.Items, i => i.IsLatest && (i.Id == cheapOld.Id || i.Id == dearMiddle.Id));

        // تاريخ المادة في التفصيل: بالوحدة والعملة نفسهما، الأحدث أولاً
        var detail = (await org.Viewer.Get<ReferencePriceDetailDto>($"{Base}/Get/{dearMiddle.Id}")).Ok();
        Assert.Equal(new[] { recent.Id, dearMiddle.Id, cheapOld.Id }, detail.History.Select(h => h.Id).ToArray());
        Assert.False(detail.IsLatest);
    }

    // ───────── التصحيح بسجل بديل ─────────

    [Fact]
    public async Task Correcting_a_historical_price_adds_a_replacement_and_keeps_the_old_record_in_the_history()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var name = $"أنبوب {Guid.NewGuid():N}"[..12];
        var wrong = await AddAsync(org, supplier, name, 1000m, DateTime.Today.AddDays(-5));

        object Fix(decimal price) => new
        {
            itemName = name, specifications = "مجدول", unit = "م", supplierId = supplier.Id, unitPrice = price, currency = "SYP", priceDate = DateTime.Today.AddDays(-5), source = 2,
            sourceDocument = "عرض رقم 7 (مصحَّح)", reason = "خطأ إدخال: صفر زائد", rowVersion = wrong.RowVersion
        };

        // بلا سبب ← 400؛ بلا صلاحية الإضافة ← 403
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Correct/{wrong.Id}", new
        {
            itemName = name, unit = "م", supplierId = supplier.Id, unitPrice = 100m, currency = "SYP", priceDate = DateTime.Today.AddDays(-5), source = 2, sourceDocument = "م", reason = " ", rowVersion = wrong.RowVersion
        })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Approver.Post<ReferencePriceDetailDto>($"{Base}/Correct/{wrong.Id}", Fix(100m))).Status);

        var fixedPrice = (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Correct/{wrong.Id}", Fix(100m))).Ok();
        Assert.Equal(1, fixedPrice.Status);                                    // البديل تاريخي بانتظار الاعتماد
        Assert.Equal(wrong.Id, fixedPrice.SupersedesId);
        Assert.Equal("خطأ إدخال: صفر زائد", fixedPrice.CorrectionReason);

        var old = (await org.Viewer.Get<ReferencePriceDetailDto>($"{Base}/Get/{wrong.Id}")).Ok();
        Assert.Equal(3, old.Status);                                           // مُستبدَل لكنه باقٍ بسعره الأصلي
        Assert.Equal(1000m, old.UnitPrice);
        Assert.Equal(fixedPrice.Id, old.SupersededById);
        Assert.False(old.CanCorrect);

        // المُستبدَل يظهر في التاريخ ويُخفى من القائمة الافتراضية ويُطلب صراحة
        Assert.Contains(fixedPrice.History, h => h.Id == wrong.Id && h.Status == 3);
        var hidden = (await org.Viewer.Get<PagedResultDto<ReferencePriceListItemDto>>($"{Base}/Search?search={Uri.EscapeDataString(name)}")).Ok();
        Assert.DoesNotContain(hidden.Items, i => i.Id == wrong.Id);
        var shown = (await org.Viewer.Get<PagedResultDto<ReferencePriceListItemDto>>($"{Base}/Search?search={Uri.EscapeDataString(name)}&includeSuperseded=true")).Ok();
        Assert.Contains(shown.Items, i => i.Id == wrong.Id);

        // السجل المُستبدَل لا يُصحَّح ثانية، وآخر سعر هو البديل
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Correct/{wrong.Id}", Fix(90m))).Status);
        Assert.True(fixedPrice.IsLatest);
    }

    [Fact]
    public async Task An_approved_price_stays_current_until_its_replacement_is_approved()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var name = $"بلاط {Guid.NewGuid():N}"[..12];
        var first = await AddAsync(org, supplier, name, 200m, DateTime.Today.AddDays(-30));
        var approvedFirst = (await org.Approver.Post<ReferencePriceDetailDto>($"{Base}/Approve/{first.Id}", new { note = (string?)null, rowVersion = first.RowVersion })).Ok();

        var replacement = (await org.Adder.Post<ReferencePriceDetailDto>($"{Base}/Correct/{first.Id}", new
        {
            itemName = name, specifications = "مجدول", unit = "م", supplierId = supplier.Id, unitPrice = 260m, currency = "SYP", priceDate = DateTime.Today.AddDays(-1), source = 2,
            sourceDocument = "عرض جديد", reason = "ارتفع السعر", rowVersion = approvedFirst.RowVersion
        })).Ok();

        // القديم المعتمد باقٍ معتمداً إلى أن يُعتمد بديله
        Assert.Equal(2, (await org.Viewer.Get<ReferencePriceDetailDto>($"{Base}/Get/{first.Id}")).Ok().Status);

        var approvedReplacement = (await org.Approver.Post<ReferencePriceDetailDto>($"{Base}/Approve/{replacement.Id}", new { note = "معتمد", rowVersion = replacement.RowVersion })).Ok();
        Assert.Equal(2, approvedReplacement.Status);
        Assert.Equal(3, (await org.Viewer.Get<ReferencePriceDetailDto>($"{Base}/Get/{first.Id}")).Ok().Status);          // صار مُستبدَلاً الآن
    }

    // ───────── الحدّ والحماية ─────────

    [Fact]
    public async Task Without_a_reference_permission_nothing_is_visible_and_a_supplier_with_prices_is_protected_from_deletion()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var price = await AddAsync(org, supplier, $"حديد {Guid.NewGuid():N}"[..12], 700m, DateTime.Today);

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Nobody.Get<PagedResultDto<ReferencePriceListItemDto>>($"{Base}/Search")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Nobody.Get<ReferencePriceDetailDto>($"{Base}/Get/{price.Id}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Viewer.Get<ReferencePriceDetailDto>($"{Base}/Get/999999")).Status);

        var delete = await api.Admin.Delete($"/api/Contractors/Delete/{supplier.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.Status);          // مرتبط بسعر مرجعي: يُعطَّل بدل حذفه (لا خطأ 500 خام)
    }

    [Fact]
    public async Task The_reference_prices_report_lists_them_for_whoever_holds_the_export_permission()
    {
        var supplier = await SupplierAsync();
        var org = await OrgAsync();
        var name = $"رمل {Guid.NewGuid():N}"[..12];
        await AddAsync(org, supplier, name, 15m, DateTime.Today);

        var report = await api.Admin.Post<JsonElement>("/api/Reports/Preview/reference-prices", new { filters = new Dictionary<string, string>() });
        Assert.Equal(HttpStatusCode.OK, report.Status);
        Assert.Contains(name, report.Raw);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Nobody.Post<JsonElement>("/api/Reports/Preview/reference-prices", new { filters = new Dictionary<string, string>() })).Status);
    }
}
