using System.Net;
using Application.DTOs.Request;
using System.Text.Json;
using Application.DTOs.Response;
using Application.Features.Pricing;
using Application.Interfaces;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// مكتب التسعير على القاعدة الحقيقية: المسار الكامل (تسجيل ← تكليف ← عروض ← مقارنة ← اختيار ← توصية ← اعتماد ← إرسال)، لا مقارنة بين وحدات أو عملات،
/// لا اختيار لغير المطابق ولو الأرخص، من أوصى لا يعتمد، تجمّد البنود، ومحضر الاتفاق (إعداد مستقل واعتماد لغير المُعِدّ).
/// </summary>
[Collection(ApiCollection.Name)]
public class PricingTests(ApiFixture api)
{
    private const string Base = "/api/PricingRequests";
    private const string Agreements = "/api/PricingAgreements";

    private sealed record Org(ApiClient Registrant, int RegistrantId, ApiClient Manager, int ManagerId, ApiClient Worker, int WorkerId, ApiClient Approver, int ApproverId, ApiClient Other);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    private async Task<(ApiClient Client, int Id)> UserAsync(string label, int roleId)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("تسعير " + label, email, roleId, api.BranchId, api.DepartmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return (api.WithToken(login.Token, id), id);
    }

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var registrantRole = await api.CreateRoleAsync("مهندس موقع تسعير " + suffix, ["RegisterPricingRequest", "ViewNotifications"], all);
        var managerRole = await api.CreateRoleAsync("مدير مكتب التسعير " + suffix,
            ["ViewPricingRequests", "AssignPricingRequest", "SendPricingResult", "ViewPricingAgreements", "PreparePricingAgreement", "ApprovePricingAgreement",
             "ViewContractors", "CreateContractor", "ExportPricingReport", "ViewNotifications"], all);
        var workerRole = await api.CreateRoleAsync("موظف تسعير " + suffix, ["PreparePricingOffers", "ViewContractors", "ViewNotifications"], all);
        var approverRole = await api.CreateRoleAsync("معتمد تسعير " + suffix, ["ViewPricingRequests", "ApprovePricingRequest", "ViewPricingAgreements", "ApprovePricingAgreement", "ViewNotifications"], all);
        var otherRole = await api.CreateRoleAsync("مهندس آخر " + suffix, ["RegisterPricingRequest"], all);

        var registrant = await UserAsync("reg", registrantRole);
        var manager = await UserAsync("mgr", managerRole);
        var worker = await UserAsync("wrk", workerRole);
        var approver = await UserAsync("apv", approverRole);
        var other = await UserAsync("oth", otherRole);
        return new Org(registrant.Client, registrant.Id, manager.Client, manager.Id, worker.Client, worker.Id, approver.Client, approver.Id, other.Client);
    }

    private async Task<ContractorResponseDto> SupplierAsync(string? name = null) =>
        (await api.Admin.Post<ContractorResponseDto>("/api/Contractors/Create", new { branchId = api.BranchId, name = name ?? $"مورد {Guid.NewGuid():N}"[..14], isActive = true })).Ok();

    /// <summary>طلب بند واحد بوحدة «طن» (أو بندين)، يُسجَّل بمهندس الموقع</summary>
    private async Task<PricingDetailDto> NewRequestAsync(Org org, int items = 1, DateTime? due = null) =>
        (await org.Registrant.Post<PricingDetailDto>($"{Base}/Create", new
        {
            title = $"توريد مواد {Guid.NewGuid():N}"[..20], description = "خارج نطاق العقد", outsideContractScope = true, dueDate = due,
            items = Enumerable.Range(1, items).Select(i => new { description = $"إسمنت {i}", specifications = "درجة 42.5", quantity = 100m * i, unit = "طن" }).ToArray()
        })).Ok();

    private async Task<PricingDetailDto> AssignedAsync(Org org, int items = 1)
    {
        var request = await NewRequestAsync(org, items);
        return (await org.Manager.Post<PricingDetailDto>($"{Base}/Assign/{request.Id}", new { assigneeUserId = org.WorkerId, rowVersion = request.RowVersion })).Ok();
    }

    private static object Line(int itemId, decimal price, string unit = "طن", int conformity = 2) => new { itemId, unitPrice = price, unit, conformity };

    private async Task<PricingDetailDto> OfferAsync(Org org, PricingDetailDto request, ContractorResponseDto supplier, string currency, params object[] lines) =>
        (await org.Worker.Post<PricingDetailDto>($"{Base}/Offers/{request.Id}", new
        {
            supplierId = supplier.Id, receivedAt = DateTime.Today, validUntil = DateTime.Today.AddDays(30), currency, supplyTerms = "تسليم موقع", lines
        })).Ok();

    // ───────── المسار الكامل ─────────

    [Fact]
    public async Task A_request_goes_from_registration_to_a_sent_result_without_ever_choosing_automatically()
    {
        var org = await OrgAsync();
        var a = await SupplierAsync();
        var b = await SupplierAsync();
        var request = await NewRequestAsync(org, items: 2);
        Assert.StartsWith("PRC-", request.Number);
        Assert.Equal(1, request.Status);
        Assert.False(request.CanAssign);                                             // مقدّمه لا يكلّف
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Registrant.Post<PricingDetailDto>($"{Base}/Assign/{request.Id}", new { assigneeUserId = org.WorkerId, rowVersion = request.RowVersion })).Status);

        // التكليف: موظف بلا صلاحية الإعداد مرفوض، والمكلَّف يصير الوحيد القادر على العمل
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Manager.Post<PricingDetailDto>($"{Base}/Assign/{request.Id}", new { assigneeUserId = org.RegistrantId, rowVersion = request.RowVersion })).Status);
        request = (await org.Manager.Post<PricingDetailDto>($"{Base}/Assign/{request.Id}", new { assigneeUserId = org.WorkerId, rowVersion = request.RowVersion })).Ok();
        Assert.Equal(2, request.Status);
        Assert.Equal(org.WorkerId, request.AssigneeUserId);
        var item1 = request.Items[0].Id;
        var item2 = request.Items[1].Id;

        // لا يعمل على العروض غير المكلَّف
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Manager.Post<PricingDetailDto>($"{Base}/Offers/{request.Id}", new { supplierId = a.Id, receivedAt = DateTime.Today, currency = "SYP", lines = new[] { Line(item1, 1m) } })).Status);

        // عرضان بالليرة: الأرخص للبند الأول غير مطابق (فلا يكون «الأرخص» ولا يُختار)، وللثاني بوحدة أخرى
        request = await OfferAsync(org, request, a, "SYP", Line(item1, 50_000m, conformity: 3), Line(item2, 60_000m, "كغ"));
        request = await OfferAsync(org, request, b, "SYP", Line(item1, 80_000m), Line(item2, 90_000m));

        var first = request.Items.Single(i => i.Id == item1);
        Assert.Equal(2, first.Rows.Count);
        Assert.Single(first.Rows, r => r.IsLowest);
        Assert.Equal(b.Id, first.Rows.Single(r => r.IsLowest).SupplierId);          // 80,000 المطابق لا 50,000 غير المطابق
        var second = request.Items.Single(i => i.Id == item2);
        var otherUnit = second.Rows.Single(r => r.SupplierId == a.Id);
        Assert.False(otherUnit.Comparable);
        Assert.NotNull(otherUnit.NotComparableReason);
        Assert.False(otherUnit.CanSelect);

        // اختيار غير المطابق أو بوحدة مخالفة أو بلا سبب: مرفوض
        var nonConforming = first.Rows.Single(r => r.SupplierId == a.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{nonConforming.LineId}", new { reason = "الأرخص" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{otherUnit.LineId}", new { reason = "س" })).Status);
        var good1 = first.Rows.Single(r => r.SupplierId == b.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{good1.LineId}", new { reason = " " })).Status);

        // التوصية قبل اختيار كل البنود مرفوضة
        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{good1.LineId}", new { reason = "مطابق وأنسب" })).Ok();
        Assert.False(request.CanRecommend);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Post<PricingDetailDto>($"{Base}/Recommend/{request.Id}", new { note = "توصية", rowVersion = request.RowVersion })).Status);
        var good2 = request.Items.Single(i => i.Id == item2).Rows.Single(r => r.SupplierId == b.Id);
        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{good2.LineId}", new { reason = "مطابق" })).Ok();
        Assert.Single(request.SelectedTotals);                                       // عملة واحدة: SYP
        Assert.Equal(80_000m * 100m + 90_000m * 200m, request.SelectedTotals.Single(t => t.Currency == "SYP").Total);

        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Recommend/{request.Id}", new { note = "نوصي بعروض المورد الثاني", rowVersion = request.RowVersion })).Ok();
        Assert.Equal(3, request.Status);

        // من أوصى لا يعتمد (ولا يملك الصلاحية أصلاً)، والمعتمِد يقرر، وبعدها يُرسَل
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Worker.Post<PricingDetailDto>($"{Base}/Decide/{request.Id}", new { decision = 1, rowVersion = request.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<PricingDetailDto>($"{Base}/Decide/{request.Id}", new { decision = 2, rowVersion = request.RowVersion })).Status);     // الرفض بلا سبب
        request = (await org.Approver.Post<PricingDetailDto>($"{Base}/Decide/{request.Id}", new { decision = 1, note = "معتمد", rowVersion = request.RowVersion })).Ok();
        Assert.Equal(4, request.Status);
        Assert.False(request.CanEdit);

        Assert.Equal(HttpStatusCode.BadRequest, (await org.Manager.Post<PricingDetailDto>($"{Base}/SendResult/{request.Id}", new { recipient = " ", rowVersion = request.RowVersion })).Status);
        request = (await org.Manager.Post<PricingDetailDto>($"{Base}/SendResult/{request.Id}", new { recipient = "مهندس الموقع", note = "بالبريد", rowVersion = request.RowVersion })).Ok();
        Assert.Equal(6, request.Status);
        Assert.Equal("مهندس الموقع", request.ResultRecipient);

        // مقدّم الطلب يرى طلبه، وغير المقدّم بلا صلاحية عرض لا يراه
        Assert.Equal(HttpStatusCode.OK, (await org.Registrant.Get<PricingDetailDto>($"{Base}/Get/{request.Id}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Other.Get<PricingDetailDto>($"{Base}/Get/{request.Id}")).Status);
        Assert.Contains((await org.Registrant.Get<PagedResultDto<PricingListItemDto>>($"{Base}/Search?mine=true")).Ok().Items, r => r.Id == request.Id);
    }

    [Fact]
    public async Task The_items_freeze_after_the_first_offer_and_a_supplier_offers_once()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var request = await AssignedAsync(org);

        // قبل العروض: تعديل البنود مسموح لصاحب التكليف
        var edited = (await org.Manager.Put<PricingDetailDto>($"{Base}/Update/{request.Id}", new
        {
            title = request.Title, description = "معدَّل", outsideContractScope = true,
            items = new[] { new { description = "إسمنت 1", specifications = "درجة 42.5", quantity = 150m, unit = "طن" } }, rowVersion = request.RowVersion
        })).Ok();
        Assert.Equal(150m, edited.Items[0].Quantity);

        request = await OfferAsync(org, edited, supplier, "SYP", Line(edited.Items[0].Id, 100m));
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Manager.Put<PricingDetailDto>($"{Base}/Update/{request.Id}", new
        {
            title = request.Title, items = new[] { new { description = "إسمنت 1", specifications = "درجة 42.5", quantity = 999m, unit = "طن" } }, rowVersion = request.RowVersion
        })).Status);
        // العنوان وحده يتغير بعد العروض
        Assert.Equal(HttpStatusCode.OK, (await org.Manager.Put<PricingDetailDto>($"{Base}/Update/{request.Id}", new
        {
            title = request.Title + " (معدَّل)", description = request.Description,
            items = request.Items.Select(i => new { description = i.Description, specifications = i.Specifications, quantity = i.Quantity, unit = i.Unit }).ToArray(), rowVersion = request.RowVersion
        })).Status);

        // مورد واحد لعرض واحد، وأسطر مكررة أو سعر سالب مرفوضة
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Post<PricingDetailDto>($"{Base}/Offers/{request.Id}", new { supplierId = supplier.Id, receivedAt = DateTime.Today, currency = "SYP", lines = new[] { Line(request.Items[0].Id, 5m) } })).Status);
        var other = await SupplierAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Post<PricingDetailDto>($"{Base}/Offers/{request.Id}", new { supplierId = other.Id, receivedAt = DateTime.Today, currency = "SYP", lines = new[] { Line(request.Items[0].Id, -1m) } })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Post<PricingDetailDto>($"{Base}/Offers/{request.Id}", new { supplierId = other.Id, receivedAt = DateTime.Today, currency = "sy", lines = new[] { Line(request.Items[0].Id, 5m) } })).Status);
    }

    [Fact]
    public async Task Currencies_are_never_mixed_each_has_its_own_lowest_and_its_own_total()
    {
        var org = await OrgAsync();
        var a = await SupplierAsync();
        var b = await SupplierAsync();
        var c = await SupplierAsync();
        var request = await AssignedAsync(org);
        var item = request.Items[0].Id;

        request = await OfferAsync(org, request, a, "SYP", Line(item, 100_000m));
        request = await OfferAsync(org, request, b, "SYP", Line(item, 90_000m));
        request = await OfferAsync(org, request, c, "USD", Line(item, 40m));

        var rows = request.Items[0].Rows;
        Assert.Equal(["SYP", "USD"], request.Items[0].Currencies);
        Assert.Equal(b.Id, rows.Single(r => r.Currency == "SYP" && r.IsLowest).SupplierId);
        Assert.Equal(c.Id, rows.Single(r => r.Currency == "USD" && r.IsLowest).SupplierId);        // الوحيد بالدولار: لا يُقارَن بالليرة

        var usd = rows.Single(r => r.SupplierId == c.Id);
        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{usd.LineId}", new { reason = "العملة الأجنبية مطلوبة" })).Ok();
        var total = Assert.Single(request.SelectedTotals);
        Assert.Equal("USD", total.Currency);
        Assert.Equal(40m * 100m, total.Total);

        // إلغاء الاختيار ثم اختيار آخر لنفس البند (سطر مختار واحد)
        var ledger = request.Items[0].Rows.Single(r => r.SupplierId == b.Id);
        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{ledger.LineId}", new { reason = "الأنسب بالليرة" })).Ok();
        Assert.Single(request.Items[0].Rows, r => r.IsSelected);
        Assert.Equal("SYP", request.SelectedTotals.Single().Currency);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Post<PricingDetailDto>($"{Base}/Unselect/{usd.LineId}", null)).Status);     // ليس مختاراً الآن
        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Unselect/{ledger.LineId}", null)).Ok();
        Assert.Empty(request.SelectedTotals);
    }

    [Fact]
    public async Task A_selected_line_cannot_be_changed_until_unselected_and_an_expired_offer_cannot_be_selected()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var request = await AssignedAsync(org);
        var item = request.Items[0].Id;
        request = await OfferAsync(org, request, supplier, "SYP", Line(item, 70m));
        var row = request.Items[0].Rows[0];
        var offer = request.Offers[0];

        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{row.LineId}", new { reason = "مطابق" })).Ok();
        offer = request.Offers[0];
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Worker.Put<PricingDetailDto>($"{Base}/Offer/{offer.Id}", new
        {
            supplierId = offer.SupplierId, receivedAt = DateTime.Today, currency = "SYP", lines = new[] { Line(item, 10m) }, rowVersion = offer.RowVersion
        })).Status);

        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Unselect/{row.LineId}", null)).Ok();
        offer = request.Offers[0];
        var updated = (await org.Worker.Put<PricingDetailDto>($"{Base}/Offer/{offer.Id}", new
        {
            supplierId = offer.SupplierId, receivedAt = DateTime.Today, validUntil = DateTime.Today.AddDays(5), currency = "SYP", lines = new[] { Line(item, 65m) }, rowVersion = offer.RowVersion
        })).Ok();
        Assert.Equal(65m, updated.Offers[0].Lines[0].UnitPrice);
    }

    [Fact]
    public async Task The_approver_can_return_a_recommendation_with_a_reason_and_the_request_goes_back_to_collecting()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var request = await AssignedAsync(org);
        var item = request.Items[0].Id;
        request = await OfferAsync(org, request, supplier, "SYP", Line(item, 70m));
        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Select/{request.Items[0].Rows[0].LineId}", new { reason = "مطابق" })).Ok();
        request = (await org.Worker.Post<PricingDetailDto>($"{Base}/Recommend/{request.Id}", new { note = "توصية", rowVersion = request.RowVersion })).Ok();

        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<PricingDetailDto>($"{Base}/Decide/{request.Id}", new { decision = 3, rowVersion = request.RowVersion })).Status);
        var returned = (await org.Approver.Post<PricingDetailDto>($"{Base}/Decide/{request.Id}", new { decision = 3, note = "أضف عرضاً ثانياً", rowVersion = request.RowVersion })).Ok();
        Assert.Equal(2, returned.Status);
        Assert.Equal("أضف عرضاً ثانياً", returned.DecisionNote);

        // يوصي من جديد ثم يُرفض بسبب
        var again = (await org.Worker.Post<PricingDetailDto>($"{Base}/Recommend/{returned.Id}", new { note = "توصية معدَّلة", rowVersion = returned.RowVersion })).Ok();
        var rejected = (await org.Approver.Post<PricingDetailDto>($"{Base}/Decide/{again.Id}", new { decision = 2, note = "الأسعار مرتفعة", rowVersion = again.RowVersion })).Ok();
        Assert.Equal(5, rejected.Status);
        Assert.Equal(HttpStatusCode.OK, (await org.Manager.Post<PricingDetailDto>($"{Base}/SendResult/{rejected.Id}", new { recipient = "مكتب العقود", rowVersion = rejected.RowVersion })).Status);
    }

    [Fact]
    public async Task A_request_is_cancelled_instead_of_deleted_before_the_recommendation()
    {
        var org = await OrgAsync();
        var request = await NewRequestAsync(org);

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Other.Post<PricingDetailDto>($"{Base}/Cancel/{request.Id}", new { rowVersion = request.RowVersion })).Status);
        var cancelled = (await org.Registrant.Post<PricingDetailDto>($"{Base}/Cancel/{request.Id}", new { note = "تسجيل خاطئ", rowVersion = request.RowVersion })).Ok();
        Assert.Equal(7, cancelled.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Manager.Post<PricingDetailDto>($"{Base}/Assign/{request.Id}", new { assigneeUserId = org.WorkerId, rowVersion = cancelled.RowVersion })).Status);
    }

    // ───────── محضر الاتفاق ─────────

    private static object AgreementBody(int supplierId, string? number, int lines = 1) => new
    {
        supplierId, number, agreementDate = DateTime.Today, technicalSpecs = "مواصفات فنية", executionMethod = "توريد وتركيب", currency = "SYP",
        lines = Enumerable.Range(1, lines).Select(i => new { description = $"بند {i}", quantity = 10m, unit = "م2", unitPrice = 5_000m }).ToArray()
    };

    [Fact]
    public async Task An_agreement_is_prepared_by_the_manager_permission_only_and_approved_by_someone_else()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var number = $"م/{Guid.NewGuid():N}"[..10];

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Worker.Post<PricingAgreementDetailDto>($"{Agreements}/Create", AgreementBody(supplier.Id, number))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Approver.Post<PricingAgreementDetailDto>($"{Agreements}/Create", AgreementBody(supplier.Id, number))).Status);

        var draft = (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Create", AgreementBody(supplier.Id, number, lines: 2))).Ok();
        Assert.StartsWith("AGR-", draft.InternalNumber);
        Assert.Equal(1, draft.Status);
        Assert.Equal(100_000m, draft.Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Create", AgreementBody(supplier.Id, number))).Status);       // رقم مكرر

        // إرسال بلا رقم رسمي مرفوض
        var noNumber = (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Create", AgreementBody(supplier.Id, null))).Ok();
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Submit/{noNumber.Id}", new { rowVersion = noNumber.RowVersion })).Status);

        var submitted = (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Submit/{draft.Id}", new { rowVersion = draft.RowVersion })).Ok();
        Assert.Equal(2, submitted.Status);
        Assert.False(submitted.CanEdit);

        // من أعدّه لا يعتمده (ولو يملك الصلاحية)، والمعتمِد غيره: الإعادة بسبب ثم التصحيح ثم الاعتماد
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Review/{submitted.Id}", new { approve = true, rowVersion = submitted.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<PricingAgreementDetailDto>($"{Agreements}/Review/{submitted.Id}", new { approve = false, rowVersion = submitted.RowVersion })).Status);
        var returned = (await org.Approver.Post<PricingAgreementDetailDto>($"{Agreements}/Review/{submitted.Id}", new { approve = false, note = "صحّح الأسعار", rowVersion = submitted.RowVersion })).Ok();
        Assert.Equal(4, returned.Status);
        Assert.True(returned.CanEdit);

        var corrected = (await org.Manager.Put<PricingAgreementDetailDto>($"{Agreements}/Update/{returned.Id}", new
        {
            supplierId = supplier.Id, number, agreementDate = DateTime.Today, currency = "SYP", technicalSpecs = "معدَّلة", executionMethod = "توريد",
            lines = new[] { new { description = "بند وحيد", quantity = 4m, unit = "م2", unitPrice = 4_000m } }, rowVersion = returned.RowVersion
        })).Ok();
        Assert.Equal(16_000m, corrected.Total);
        Assert.Single(corrected.Lines);

        var resubmitted = (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Submit/{corrected.Id}", new { rowVersion = corrected.RowVersion })).Ok();
        var approved = (await org.Approver.Post<PricingAgreementDetailDto>($"{Agreements}/Review/{resubmitted.Id}", new { approve = true, note = "معتمد", rowVersion = resubmitted.RowVersion })).Ok();
        Assert.Equal(3, approved.Status);
        Assert.NotNull(approved.ApprovedByName);

        // المعتمد لا يُعدَّل ولا يُلغى
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Manager.Put<PricingAgreementDetailDto>($"{Agreements}/Update/{approved.Id}", AgreementBody(supplier.Id, number))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Cancel/{approved.Id}", new { rowVersion = approved.RowVersion })).Status);

        // الحدّ: يراه من يملك صلاحية العرض في فرعه، ومن لا يملك أي صلاحية محاضر لا يراه
        Assert.Equal(HttpStatusCode.OK, (await org.Approver.Get<PricingAgreementDetailDto>($"{Agreements}/Get/{approved.Id}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Outsider.Get<PricingAgreementDetailDto>($"{Agreements}/Get/{approved.Id}")).Status);
        Assert.Contains((await org.Manager.Get<PagedResultDto<PricingAgreementListItemDto>>($"{Agreements}/Search?status=3")).Ok().Items, x => x.Id == approved.Id);
    }

    [Fact]
    public async Task A_supplier_used_in_an_offer_or_an_agreement_is_not_deleted()
    {
        var org = await OrgAsync();
        var supplier = await SupplierAsync();
        var request = await AssignedAsync(org);
        await OfferAsync(org, request, supplier, "SYP", Line(request.Items[0].Id, 10m));
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Delete($"/api/Contractors/Delete/{supplier.Id}")).Status);

        var other = await SupplierAsync();
        (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Create", AgreementBody(other.Id, null))).Ok();
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Delete($"/api/Contractors/Delete/{other.Id}")).Status);
    }

    // ───────── التذكير والتقارير ─────────

    [Fact]
    public async Task An_overdue_request_is_notified_once_per_due_date_and_its_status_never_changes_by_time()
    {
        var org = await OrgAsync();
        var request = await NewRequestAsync(org, due: DateTime.Today.AddDays(-3));
        Assert.True(request.IsOverdue);

        async Task<int> RunAsync()
        {
            await using var scope = api.Services.CreateAsyncScope();
            return await PricingReminders.RunAsync(scope.ServiceProvider.GetRequiredService<IPricingService>(), scope.ServiceProvider.GetRequiredService<IUserPermissionService>(),
                scope.ServiceProvider.GetRequiredService<INotificationService>(), DateTime.Now);
        }

        Assert.True(await RunAsync() >= 1);
        Assert.Equal(0, await RunAsync());                                          // مرة لكل موعد

        // تغيير الموعد إلى موعد متأخر آخر يسمح بتنبيه جديد (يُعاد قراءة نسخة الصف بعد تحديث التذكير له)
        var fresh = (await org.Registrant.Get<PricingDetailDto>($"{Base}/Get/{request.Id}")).Ok();
        (await org.Registrant.Put<PricingDetailDto>($"{Base}/Update/{fresh.Id}", new
        {
            title = fresh.Title, description = fresh.Description, outsideContractScope = true, dueDate = DateTime.Today.AddDays(-1),
            items = fresh.Items.Select(i => new { description = i.Description, specifications = i.Specifications, quantity = i.Quantity, unit = i.Unit }).ToArray(), rowVersion = fresh.RowVersion
        })).Ok();
        Assert.True(await RunAsync() >= 1);

        Assert.Equal(1, (await org.Registrant.Get<PricingDetailDto>($"{Base}/Get/{request.Id}")).Ok().Status);      // لم تتغير الحالة بالتنبيه
        var overdue = (await org.Registrant.Get<PagedResultDto<PricingListItemDto>>($"{Base}/Search?overdueOnly=true&mine=true")).Ok();
        Assert.Contains(overdue.Items, r => r.Id == request.Id);
    }

    [Fact]
    public async Task The_pricing_reports_list_the_requests_and_the_agreements_for_whoever_holds_the_export_permission()
    {
        var org = await OrgAsync();
        var request = await NewRequestAsync(org);
        var supplier = await SupplierAsync();
        var number = $"PA-{Guid.NewGuid():N}"[..11];
        (await org.Manager.Post<PricingAgreementDetailDto>($"{Agreements}/Create", AgreementBody(supplier.Id, number))).Ok();

        var requests = await org.Manager.Post<JsonElement>("/api/Reports/Preview/pricing-requests", new { filters = new Dictionary<string, string>() });
        Assert.Equal(HttpStatusCode.OK, requests.Status);
        Assert.Contains(request.Number, requests.Raw);

        var agreements = await org.Manager.Post<JsonElement>("/api/Reports/Preview/pricing-agreements", new { filters = new Dictionary<string, string>() });
        Assert.Equal(HttpStatusCode.OK, agreements.Status);
        Assert.Contains(number, agreements.Raw);

        // بلا صلاحية التقارير لا يُسحب، وبلا صلاحية عرض المحاضر لا يُسحب تقريرها
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Registrant.Post<JsonElement>("/api/Reports/Preview/pricing-requests", new { filters = new Dictionary<string, string>() })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Worker.Post<JsonElement>("/api/Reports/Preview/pricing-agreements", new { filters = new Dictionary<string, string>() })).Status);
    }

    [Fact]
    public async Task The_assignee_list_holds_only_those_who_can_prepare_offers_and_only_for_whoever_may_assign()
    {
        var org = await OrgAsync();
        var list = (await org.Manager.Get<List<PricingAssigneeDto>>($"{Base}/Assignees")).Ok();
        Assert.Contains(list, u => u.Id == org.WorkerId);                            // يحمل إعداد العروض
        Assert.DoesNotContain(list, u => u.Id == org.RegistrantId);                  // يسجّل فقط
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Registrant.Get<List<PricingAssigneeDto>>($"{Base}/Assignees")).Status);
    }
}
