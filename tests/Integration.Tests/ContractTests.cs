using System.Net;
using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// العقود والمقاولون على القاعدة الحقيقية: المقاولون والمعايير (فرادة الاسم وحارس الحذف)، العقد المباشر (سريان يشترط البيانات، تجمّد القيمة،
/// ملحق، لجنة برئيس واحد، محتجز وإفراج نهائي)، المناقصة (عروض وإرساء واحد)، تقييم المقاول (غير المُعدّ يراجع)، وحدّ الرؤية.
/// </summary>
[Collection(ApiCollection.Name)]
public class ContractTests(ApiFixture api)
{
    private const string Base = "/api/Contracts";

    private sealed record Org(ApiClient Office, int OfficeUserId, ApiClient Engineer, int EngineerId, ApiClient SelfReviewer, int SelfReviewerId, ApiClient Stranger);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    private async Task<(ApiClient Client, int Id)> UserAsync(string label, int roleId)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("عقود " + label, email, roleId, api.BranchId, api.DepartmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return (api.WithToken(login.Token, id), id);
    }

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var officeRole = await api.CreateRoleAsync("مكتب عقود " + suffix,
            ["ViewProjects", "ViewBranchProjects", "ViewContracts", "CreateContract", "EditContract", "ManageContractCommittees", "AddContractAmendment", "RecordContractRetention",
             "ViewContractors", "CreateContractor", "EditContractor", "DeleteContractor", "ViewContractorEvaluations", "ReviewContractorEvaluation",
             "ViewEvaluationCriteria", "CreateEvaluationCriterion", "EditEvaluationCriterion", "DeleteEvaluationCriterion", "ViewNotifications"], all);
        var engineerRole = await api.CreateRoleAsync("مهندس موقع " + suffix,
            ["ViewProjects", "EvaluateContractor", "ViewContractors", "ViewEvaluationCriteria", "ViewNotifications"], all);
        var selfRole = await api.CreateRoleAsync("مهندس ومراجع " + suffix,
            ["ViewProjects", "EvaluateContractor", "ReviewContractorEvaluation", "ViewContractors", "ViewEvaluationCriteria"], all);
        var strangerRole = await api.CreateRoleAsync("غريب عقود " + suffix, ["ViewProjects", "ViewContracts"], all);

        var office = await UserAsync("office", officeRole);
        var engineer = await UserAsync("eng", engineerRole);
        var self = await UserAsync("self", selfRole);
        var stranger = await UserAsync("str", strangerRole);
        return new Org(office.Client, office.Id, engineer.Client, engineer.Id, self.Client, self.Id, stranger.Client);
    }

    /// <summary>مشروع جديد بمُعِدّ الجدول منشئاً، والمهندسان مشاركان فيه</summary>
    private async Task<int> NewProjectAsync(Org org)
    {
        var project = (await api.Preparer.Post<ProjectResponseDto>("/api/Projects/Create", new ProjectRequestDto
        {
            Name = $"مشروع عقد {Guid.NewGuid():N}"[..28], Beneficiary = "جهة اختبار", DepartmentId = api.DepartmentId
        })).Ok();
        foreach (var userId in new[] { org.EngineerId, org.SelfReviewerId })
            (await api.Preparer.Post<JsonElement>($"/api/Projects/Members/{project.Id}", new { userId, title = "مهندس موقع" })).Ok();
        return project.Id;
    }

    private async Task<ContractorResponseDto> NewContractorAsync(Org org, string? name = null) =>
        (await org.Office.Post<ContractorResponseDto>("/api/Contractors/Create", new { name = name ?? $"مقاول {Guid.NewGuid():N}"[..14], isActive = true })).Ok();

    private static string Official() => $"ع/{Guid.NewGuid():N}"[..10];

    /// <summary>عقد مباشر مكتمل البيانات (رقم ومتعاقد وتاريخ وقيمة) ويبقى «قيد التعاقد»</summary>
    private async Task<ContractDetailDto> NewDirectAsync(Org org, int projectId, int contractorId, decimal value = 1_000_000m, int? warrantyMonths = null) =>
        (await org.Office.Post<ContractDetailDto>($"{Base}/Create", new
        {
            projectId, method = 1, contractorId, number = Official(), contractDate = DateTime.Today.AddDays(-3), startDate = DateTime.Today, durationDays = 200,
            value, currency = "SYP", scope = "أعمال البناء", warrantyMonths
        })).Ok();

    private async Task<ContractDetailDto> ActivateAsync(Org org, ContractDetailDto contract) =>
        (await org.Office.Post<ContractDetailDto>($"{Base}/Status/{contract.Id}", new { status = 2, rowVersion = contract.RowVersion })).Ok();

    private async Task<ContractDetailDto> ActiveContractAsync(Org org, int projectId, int contractorId) => await ActivateAsync(org, await NewDirectAsync(org, projectId, contractorId));

    // ───────── المقاولون ومعايير التقييم ─────────

    [Fact]
    public async Task Contractors_and_criteria_are_branch_data_unique_by_name_and_protected_from_deletion_once_used()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var contractor = await NewContractorAsync(org);

        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractorResponseDto>("/api/Contractors/Create", new { name = contractor.Name })).Status);   // اسم مكرر
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<ContractorResponseDto>("/api/Contractors/Create", new { name = "مقاول عابر" })).Status);
        Assert.Contains((await org.Engineer.Get<List<ContractorResponseDto>>("/api/Contractors/GetAll")).Ok(), c => c.Id == contractor.Id);

        var updated = (await org.Office.Put<ContractorResponseDto>($"/api/Contractors/Update/{contractor.Id}", new { name = contractor.Name, phone = "0999", isActive = true, rowVersion = contractor.RowVersion })).Ok();
        Assert.Equal("0999", updated.Phone);

        var criterion = (await org.Office.Post<EvaluationCriterionResponseDto>("/api/EvaluationCriteria/Create", new { name = $"الجودة {Guid.NewGuid():N}"[..14], weight = 30, sortOrder = 1 })).Ok();
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<EvaluationCriterionResponseDto>("/api/EvaluationCriteria/Create", new { name = criterion.Name })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<EvaluationCriterionResponseDto>("/api/EvaluationCriteria/Create", new { name = "وزن خاطئ", weight = 101 })).Status);

        // مرتبط بعقد: لا يُحذف
        await NewDirectAsync(org, projectId, contractor.Id);
        var blocked = await org.Office.Delete($"/api/Contractors/Delete/{contractor.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, blocked.Status);

        var free = await NewContractorAsync(org);
        Assert.Equal(HttpStatusCode.OK, (await org.Office.Delete($"/api/Contractors/Delete/{free.Id}")).Status);
        Assert.Equal(HttpStatusCode.OK, (await org.Office.Delete($"/api/EvaluationCriteria/Delete/{criterion.Id}")).Status);
    }

    // ───────── العقد المباشر ─────────

    [Fact]
    public async Task A_direct_contract_activates_only_with_its_data_and_then_freezes_its_core_fields()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var contractor = await NewContractorAsync(org);

        // بلا رقم رسمي ولا متعاقد: لا سريان
        var bare = (await org.Office.Post<ContractDetailDto>($"{Base}/Create", new { projectId, method = 1, currency = "SYP" })).Ok();
        Assert.Equal(1, bare.Status);
        Assert.DoesNotContain(2, bare.AllowedStatuses);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Status/{bare.Id}", new { status = 2, rowVersion = bare.RowVersion })).Status);

        var contract = await NewDirectAsync(org, projectId, contractor.Id);
        Assert.StartsWith("CON-", contract.Number);
        Assert.Equal(1_000_000m, contract.EffectiveValue);

        // رقم رسمي مكرر في الفرع
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Create",
            new { projectId, method = 1, contractorId = contractor.Id, number = contract.OfficialNumber, currency = "SYP" })).Status);

        // الانتقال المباشر إلى «منجز» غير مسموح، والإلغاء يشترط سبباً
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Status/{contract.Id}", new { status = 3, rowVersion = contract.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Status/{contract.Id}", new { status = 5, rowVersion = contract.RowVersion })).Status);

        var active = await ActivateAsync(org, contract);
        Assert.Equal(2, active.Status);

        // بعد السريان: القيمة والمدة والمتعاقد والطبيعة متجمّدة
        var frozen = await org.Office.Put<ContractDetailDto>($"{Base}/Update/{active.Id}", new
        {
            projectId, method = 1, contractorId = contractor.Id, number = active.OfficialNumber, contractDate = active.ContractDate, durationDays = active.DurationDays,
            value = 2_000_000m, currency = "SYP", rowVersion = active.RowVersion
        });
        Assert.Equal(HttpStatusCode.BadRequest, frozen.Status);

        // أما الملاحظات والنطاق فتُعدَّل
        var edited = (await org.Office.Put<ContractDetailDto>($"{Base}/Update/{active.Id}", new
        {
            projectId, method = 1, contractorId = contractor.Id, number = active.OfficialNumber, contractDate = active.ContractDate, startDate = active.StartDate,
            durationDays = active.DurationDays, value = active.Value, currency = "SYP", scope = "نطاق معدَّل", rowVersion = active.RowVersion
        })).Ok();
        Assert.Equal("نطاق معدَّل", edited.Scope);

        // تعديل بنسخة قديمة يُرفض بوضوح
        var stale = await org.Office.Put<ContractDetailDto>($"{Base}/Update/{active.Id}", new
        {
            projectId, method = 1, contractorId = contractor.Id, number = active.OfficialNumber, contractDate = active.ContractDate, startDate = active.StartDate,
            durationDays = active.DurationDays, value = active.Value, currency = "SYP", scope = "نطاق آخر", rowVersion = active.RowVersion
        });
        Assert.Equal(HttpStatusCode.BadRequest, stale.Status);
    }

    [Fact]
    public async Task Amendments_change_the_effective_value_and_duration_and_the_contract_value_itself_stays()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var active = await ActiveContractAsync(org, projectId, (await NewContractorAsync(org)).Id);

        var withAmendment = (await org.Office.Post<ContractDetailDto>($"{Base}/Amendments/{active.Id}",
            new { title = "ملحق إضافة أعمال", valueChange = 250_000m, durationChangeDays = 30, approvedAt = DateTime.Today })).Ok();
        Assert.Single(withAmendment.Amendments);
        Assert.Equal(1, withAmendment.Amendments[0].Number);
        Assert.Equal(1_000_000m, withAmendment.Value);
        Assert.Equal(1_250_000m, withAmendment.EffectiveValue);
        Assert.Equal(230, withAmendment.EffectiveDurationDays);

        var second = (await org.Office.Post<ContractDetailDto>($"{Base}/Amendments/{active.Id}",
            new { title = "ملحق خصم", valueChange = -50_000m, approvedAt = DateTime.Today })).Ok();
        Assert.Equal(2, second.Amendments.Max(a => a.Number));
        Assert.Equal(1_200_000m, second.EffectiveValue);

        // ينزل بالقيمة إلى ما دون الصفر، أو بلا أثر ولا شرح، أو في المستقبل: مرفوض
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Amendments/{active.Id}", new { title = "x", valueChange = -9_000_000m, approvedAt = DateTime.Today })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Amendments/{active.Id}", new { title = "x", approvedAt = DateTime.Today })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Amendments/{active.Id}", new { title = "x", valueChange = 1m, approvedAt = DateTime.Today.AddDays(1) })).Status);

        // قيد التعاقد: لا ملاحق
        var draft = await NewDirectAsync(org, projectId, (await NewContractorAsync(org)).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Amendments/{draft.Id}", new { title = "x", valueChange = 1m, approvedAt = DateTime.Today })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<ContractDetailDto>($"{Base}/Amendments/{active.Id}", new { title = "x", valueChange = 1m, approvedAt = DateTime.Today })).Status);
    }

    [Fact]
    public async Task The_committee_has_exactly_one_chair_and_editing_it_replaces_the_members_with_an_audit_trail()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var contract = await NewDirectAsync(org, projectId, (await NewContractorAsync(org)).Id);

        var bad = new { formedAt = DateTime.Today, members = new object[] { new { userId = org.OfficeUserId, capacity = "مسؤول العقود" }, new { externalName = "ممثل", capacity = "مساحة" } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Committees/{contract.Id}", bad)).Status);                 // بلا رئيس

        var formed = (await org.Office.Post<ContractDetailDto>($"{Base}/Committees/{contract.Id}", new
        {
            formedAt = DateTime.Today, decisionNumber = "قرار 12", recommendations = "توصيات أولية",
            members = new object[]
            {
                new { userId = org.OfficeUserId, capacity = "رئيس قسم الإشراف", isChair = true },
                new { userId = org.EngineerId, capacity = "مسؤول العقود" },
                new { externalName = "ممثل المساحة", capacity = "المساحة والكميات" }
            }
        })).Ok();
        var committee = Assert.Single(formed.Committees);
        Assert.Equal(3, committee.Members.Count);
        Assert.Single(committee.Members, m => m.IsChair);
        Assert.Contains(committee.Members, m => m.IsExternal && m.Name == "ممثل المساحة");

        // موظف مكرر أو رئيسان: مرفوض
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Put<ContractDetailDto>($"{Base}/Committee/{committee.Id}", new
        {
            formedAt = DateTime.Today, rowVersion = committee.RowVersion,
            members = new object[] { new { userId = org.OfficeUserId, capacity = "أ", isChair = true }, new { userId = org.OfficeUserId, capacity = "ب" } }
        })).Status);

        // تعديل: رئيس جديد وعضوان (الرئيس القديم يُحذف قبل إضافة الجديد)
        var replaced = (await org.Office.Put<ContractDetailDto>($"{Base}/Committee/{committee.Id}", new
        {
            formedAt = DateTime.Today, decisionNumber = "قرار 13", recommendations = "توصيات نهائية", rowVersion = committee.RowVersion,
            members = new object[] { new { userId = org.EngineerId, capacity = "رئيس", isChair = true }, new { userId = org.OfficeUserId, capacity = "مسؤول العقود" } }
        })).Ok();
        var after = Assert.Single(replaced.Committees);
        Assert.Equal(2, after.Members.Count);
        Assert.Equal(org.EngineerId, after.Members.Single(m => m.IsChair).UserId);
        Assert.Equal("قرار 13", after.DecisionNumber);

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<ContractDetailDto>($"{Base}/Committees/{contract.Id}", bad)).Status);
    }

    [Fact]
    public async Task Retention_is_manual_and_a_release_is_final_while_continuing_the_retention_can_be_decided_again()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var active = await ActiveContractAsync(org, projectId, (await NewContractorAsync(org)).Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Retention/{active.Id}", new { amount = 0, withheldAt = DateTime.Today })).Status);
        var held = (await org.Office.Post<ContractDetailDto>($"{Base}/Retention/{active.Id}", new { amount = 80_000m, withheldAt = DateTime.Today.AddDays(-10), note = "احتجاز أول" })).Ok();
        held = (await org.Office.Post<ContractDetailDto>($"{Base}/Retention/{active.Id}", new { amount = 20_000m, withheldAt = DateTime.Today.AddDays(-5) })).Ok();
        Assert.Equal(100_000m, held.StillWithheld);
        var first = held.Retention.OrderBy(r => r.Id).First();
        Assert.Equal(1, first.State);

        // قرار بلا جهة اعتماد أو قبل تاريخ الاحتجاز: مرفوض
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/RetentionDecision/{first.Id}", new { decision = 2, decidedAt = DateTime.Today, approvingAuthority = " " })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/RetentionDecision/{first.Id}", new { decision = 2, decidedAt = DateTime.Today.AddDays(-11), approvingAuthority = "ج" })).Status);

        var continued = (await org.Office.Post<ContractDetailDto>($"{Base}/RetentionDecision/{first.Id}", new { decision = 2, decidedAt = DateTime.Today.AddDays(-1), approvingAuthority = "إدارة المشاريع" })).Ok();
        Assert.Equal(2, continued.Retention.Single(r => r.Id == first.Id).State);
        Assert.Equal(100_000m, continued.StillWithheld);

        var released = (await org.Office.Post<ContractDetailDto>($"{Base}/RetentionDecision/{first.Id}", new { decision = 1, decidedAt = DateTime.Today, approvingAuthority = "إدارة المشاريع", note = "انتهى الضمان" })).Ok();
        Assert.Equal(3, released.Retention.Single(r => r.Id == first.Id).State);
        Assert.Equal(20_000m, released.StillWithheld);
        Assert.Equal(2, released.Retention.Single(r => r.Id == first.Id).Decisions.Count);

        // الإفراج نهائي
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/RetentionDecision/{first.Id}", new { decision = 2, decidedAt = DateTime.Today, approvingAuthority = "ج" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<ContractDetailDto>($"{Base}/Retention/{active.Id}", new { amount = 5m, withheldAt = DateTime.Today })).Status);
    }

    // ───────── المناقصة ─────────

    [Fact]
    public async Task A_tender_collects_offers_awards_one_and_activates_for_the_awarded_contractor()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var a = await NewContractorAsync(org);
        var b = await NewContractorAsync(org);

        // التنفيذ المباشر لا عروض فيه، وغير المباشر لا يقبل متعاقداً مباشرة
        var direct = await NewDirectAsync(org, projectId, a.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Offers/{direct.Id}", new { contractorId = a.Id, amount = 1m, receivedAt = DateTime.Today })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Create", new { projectId, method = 2, contractorId = a.Id, currency = "SYP" })).Status);
        // بيانات المناقصة لا تُملأ في المباشر
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Create", new { projectId, method = 1, tenderNumber = "م/1", currency = "SYP" })).Status);

        var tender = (await org.Office.Post<ContractDetailDto>($"{Base}/Create", new
        {
            projectId, method = 2, number = Official(), contractDate = DateTime.Today, tenderNumber = "م/55", tenderSentAt = DateTime.Today.AddDays(-20), tenderOpenedAt = DateTime.Today.AddDays(-5), currency = "SYP"
        })).Ok();
        Assert.True(tender.CanAddOffer);

        tender = (await org.Office.Post<ContractDetailDto>($"{Base}/Offers/{tender.Id}", new { contractorId = a.Id, amount = 900_000m, receivedAt = DateTime.Today.AddDays(-6) })).Ok();
        tender = (await org.Office.Post<ContractDetailDto>($"{Base}/Offers/{tender.Id}", new { contractorId = b.Id, amount = 850_000m, receivedAt = DateTime.Today.AddDays(-6), note = "الأرخص" })).Ok();
        Assert.Equal(2, tender.Offers.Count);
        Assert.Equal(b.Id, tender.Offers[0].ContractorId);       // الأقل مبلغاً أولاً (عرض فقط؛ لا ترجيح)
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Offers/{tender.Id}", new { contractorId = a.Id, amount = 1m, receivedAt = DateTime.Today })).Status);   // عرض مكرر

        // السريان قبل الإرساء مرفوض
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Status/{tender.Id}", new { status = 2, rowVersion = tender.RowVersion })).Status);

        // الإرساء قبل فض المناقصة مرفوض، ثم على العرض الأرخص بقرار
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Award/{tender.Offers[0].Id}", new { awardedAt = DateTime.Today.AddDays(-9), rowVersion = tender.RowVersion })).Status);
        var awarded = (await org.Office.Post<ContractDetailDto>($"{Base}/Award/{tender.Offers[0].Id}", new { awardedAt = DateTime.Today, note = "قرار اللجنة", rowVersion = tender.RowVersion })).Ok();
        Assert.Equal(b.Id, awarded.ContractorId);
        Assert.Equal(850_000m, awarded.Value);                    // نُسخت قيمة العرض لأن العقد بلا قيمة
        Assert.Single(awarded.Offers, o => o.IsAwarded);
        Assert.False(awarded.CanAddOffer);

        // إرساء ثانٍ أو عرض بعده: مرفوض
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Award/{awarded.Offers.First(o => !o.IsAwarded).Id}", new { rowVersion = awarded.RowVersion })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Offers/{awarded.Id}", new { contractorId = (await NewContractorAsync(org)).Id, amount = 1m, receivedAt = DateTime.Today })).Status);

        var active = (await org.Office.Post<ContractDetailDto>($"{Base}/Status/{awarded.Id}", new { status = 2, rowVersion = awarded.RowVersion })).Ok();
        Assert.Equal(2, active.Status);
        // الإنجاز يحفظ سجل الحالة
        var done = (await org.Office.Post<ContractDetailDto>($"{Base}/Status/{active.Id}", new { status = 3, rowVersion = active.RowVersion })).Ok();
        Assert.Equal(3, done.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Status/{done.Id}", new { status = 2, rowVersion = done.RowVersion })).Status);
    }

    // ───────── تقييم المقاول ─────────

    [Fact]
    public async Task A_contractor_evaluation_goes_draft_to_reviewed_by_someone_else_and_is_then_frozen_in_the_contractor_file()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var contractor = await NewContractorAsync(org);
        var contract = await ActiveContractAsync(org, projectId, contractor.Id);
        var quality = (await org.Office.Post<EvaluationCriterionResponseDto>("/api/EvaluationCriteria/Create", new { name = $"الجودة {Guid.NewGuid():N}"[..14], weight = 40, sortOrder = 1 })).Ok();
        var speed = (await org.Office.Post<EvaluationCriterionResponseDto>("/api/EvaluationCriteria/Create", new { name = $"السرعة {Guid.NewGuid():N}"[..14], sortOrder = 2 })).Ok();

        // المهندس يعدّ مسودة بدرجتين (واحدة بلا درجة بعد): درجة خارج 0–100 أو معيار مكرر مرفوضان
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Post<ContractDetailDto>($"{Base}/Evaluations/{contract.Id}",
            new { evaluationDate = DateTime.Today, scores = new[] { new { criterionId = quality.Id, score = 120m } } })).Status);
        var withDraft = (await org.Engineer.Post<ContractDetailDto>($"{Base}/Evaluations/{contract.Id}", new
        {
            evaluationDate = DateTime.Today, notes = "تقييم ربع سنوي",
            scores = new[] { new { criterionId = quality.Id, score = (decimal?)85m, note = "جيد" }, new { criterionId = speed.Id, score = (decimal?)null, note = "" } }
        })).Ok();
        var draft = Assert.Single(withDraft.Evaluations);
        Assert.Equal(1, draft.Status);
        Assert.True(draft.CanEdit);
        Assert.False(draft.CanReview);
        Assert.Equal(2, draft.Scores.Count);

        // تعديل المسودة: يُحذف معيار ويُحدَّث آخر (تبقى النسخة من الاسم والوزن)
        var edited = (await org.Engineer.Put<ContractDetailDto>($"{Base}/Evaluation/{draft.Id}", new
        {
            evaluationDate = DateTime.Today, notes = "تعديل", rowVersion = draft.RowVersion, scores = new[] { new { criterionId = quality.Id, score = (decimal?)90m, note = "ممتاز" } }
        })).Ok().Evaluations.Single();
        Assert.Single(edited.Scores);
        Assert.Equal(40m, edited.Scores[0].Weight);
        Assert.Equal(90m, edited.Scores[0].Score);

        // غير المشارك لا يعدّ، ومن يراها بلا صلاحية لا يجدها
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Stranger.Post<ContractDetailDto>($"{Base}/Evaluations/{contract.Id}", new { evaluationDate = DateTime.Today, scores = Array.Empty<object>() })).Status);

        // الإرسال إلى مكتب العقود، ومن أعدّه لا يراجعه (حتى لو يملك صلاحية المراجعة)
        var submitted = (await org.Engineer.Post<ContractDetailDto>($"{Base}/Evaluation/{edited.Id}/Submit", new { rowVersion = edited.RowVersion })).Ok().Evaluations.Single();
        Assert.Equal(2, submitted.Status);
        Assert.False(submitted.CanEdit);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Put<ContractDetailDto>($"{Base}/Evaluation/{submitted.Id}",
            new { evaluationDate = DateTime.Today, rowVersion = submitted.RowVersion, scores = Array.Empty<object>() })).Status);

        var selfDraft = (await org.SelfReviewer.Post<ContractDetailDto>($"{Base}/Evaluations/{contract.Id}",
            new { evaluationDate = DateTime.Today, scores = new[] { new { criterionId = quality.Id, score = (decimal?)70m } } })).Ok().Evaluations.Single(e => e.EvaluatedByUserId == org.SelfReviewerId);
        var selfSubmitted = (await org.SelfReviewer.Post<ContractDetailDto>($"{Base}/Evaluation/{selfDraft.Id}/Submit", new { rowVersion = selfDraft.RowVersion })).Ok().Evaluations.Single(e => e.Id == selfDraft.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.SelfReviewer.Post<ContractDetailDto>($"{Base}/Evaluation/{selfDraft.Id}/Review", new { rowVersion = selfSubmitted.RowVersion })).Status);

        // الإعادة بلا سبب مرفوضة، ثم بسبب فتعود مسودة
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Evaluation/{submitted.Id}/Return", new { note = " ", rowVersion = submitted.RowVersion })).Status);
        var returned = (await org.Office.Post<ContractDetailDto>($"{Base}/Evaluation/{submitted.Id}/Return", new { note = "أكمل المعيار الثاني", rowVersion = submitted.RowVersion })).Ok()
            .Evaluations.Single(e => e.Id == submitted.Id);
        Assert.Equal(1, returned.Status);
        Assert.Equal("أكمل المعيار الثاني", returned.ReviewNote);

        var resubmitted = (await org.Engineer.Post<ContractDetailDto>($"{Base}/Evaluation/{returned.Id}/Submit", new { rowVersion = returned.RowVersion })).Ok().Evaluations.Single(e => e.Id == returned.Id);
        var reviewed = (await org.Office.Post<ContractDetailDto>($"{Base}/Evaluation/{resubmitted.Id}/Review", new { note = "حُفظ", rowVersion = resubmitted.RowVersion })).Ok()
            .Evaluations.Single(e => e.Id == resubmitted.Id);
        Assert.Equal(3, reviewed.Status);
        Assert.NotNull(reviewed.ReviewedByName);

        // المراجَع لا يُعدَّل ولا يُحذف ولا يُراجَع ثانية
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Put<ContractDetailDto>($"{Base}/Evaluation/{reviewed.Id}",
            new { evaluationDate = DateTime.Today, rowVersion = reviewed.RowVersion, scores = Array.Empty<object>() })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Delete($"{Base}/Evaluation/{reviewed.Id}")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<ContractDetailDto>($"{Base}/Evaluation/{reviewed.Id}/Review", new { rowVersion = reviewed.RowVersion })).Status);

        // ملف المقاول: المراجَع فقط، ولمن يملك «عرض التقييمات»
        var history = (await org.Office.Get<List<ContractorEvaluationHistoryDto>>($"/api/Contractors/Evaluations/{contractor.Id}")).Ok();
        var entry = Assert.Single(history);
        Assert.Equal(reviewed.Id, entry.Id);
        Assert.Equal(contract.Id, entry.ContractId);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Get<List<ContractorEvaluationHistoryDto>>($"/api/Contractors/Evaluations/{contractor.Id}")).Status);

        // معيار عليه درجات: لا يُحذف
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Delete($"/api/EvaluationCriteria/Delete/{quality.Id}")).Status);
    }

    [Fact]
    public async Task An_evaluation_draft_is_hidden_from_everyone_but_its_author_and_deleted_by_the_author()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var contract = await ActiveContractAsync(org, projectId, (await NewContractorAsync(org)).Id);
        var criterion = (await org.Office.Post<EvaluationCriterionResponseDto>("/api/EvaluationCriteria/Create", new { name = $"النظافة {Guid.NewGuid():N}"[..14] })).Ok();

        var draft = (await org.Engineer.Post<ContractDetailDto>($"{Base}/Evaluations/{contract.Id}",
            new { evaluationDate = DateTime.Today, scores = new[] { new { criterionId = criterion.Id, score = (decimal?)60m } } })).Ok().Evaluations.Single();

        // مكتب العقود يملك «عرض التقييمات» فيرى كل التقييمات؛ المهندس الآخر لا يرى مسودة غيره
        Assert.Contains((await org.Office.Get<ContractDetailDto>($"{Base}/Get/{contract.Id}")).Ok().Evaluations, e => e.Id == draft.Id);
        var others = (await org.SelfReviewer.Get<ContractDetailDto>($"{Base}/Get/{contract.Id}")).Ok();
        Assert.DoesNotContain(others.Evaluations, e => e.Id == draft.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await org.SelfReviewer.Delete($"{Base}/Evaluation/{draft.Id}")).Status);

        var afterDelete = (await org.Engineer.Delete($"{Base}/Evaluation/{draft.Id}"));
        Assert.Equal(HttpStatusCode.OK, afterDelete.Status);
        Assert.DoesNotContain((await org.Office.Get<ContractDetailDto>($"{Base}/Get/{contract.Id}")).Ok().Evaluations, e => e.Id == draft.Id);
    }

    // ───────── الحدود والرؤية ─────────

    [Fact]
    public async Task A_user_outside_the_project_does_not_see_its_contracts_and_the_list_respects_the_same_boundary()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var contract = await NewDirectAsync(org, projectId, (await NewContractorAsync(org)).Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Stranger.Get<ContractDetailDto>($"{Base}/Get/{contract.Id}")).Status);
        Assert.DoesNotContain((await org.Stranger.Get<PagedResultDto<ContractListItemDto>>($"{Base}/Search?projectId={projectId}")).Ok().Items, c => c.Id == contract.Id);
        Assert.Contains((await org.Office.Get<PagedResultDto<ContractListItemDto>>($"{Base}/Search?projectId={projectId}")).Ok().Items, c => c.Id == contract.Id);

        // الفلاتر
        var byStatus = (await org.Office.Get<PagedResultDto<ContractListItemDto>>($"{Base}/Search?projectId={projectId}&status=1&method=1")).Ok();
        Assert.Contains(byStatus.Items, c => c.Id == contract.Id);
        var other = (await org.Office.Get<PagedResultDto<ContractListItemDto>>($"{Base}/Search?projectId={projectId}&status=3")).Ok();
        Assert.DoesNotContain(other.Items, c => c.Id == contract.Id);

        // من لا يملك أي صلاحية عقود محظور من القائمة نفسها
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Outsider.Get<PagedResultDto<ContractListItemDto>>($"{Base}/Search")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Office.Get<ContractDetailDto>($"{Base}/Get/999999")).Status);
    }

    [Fact]
    public async Task The_warranty_is_entered_by_hand_with_a_suggested_end_and_the_list_flags_the_ones_ending_soon()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var contractor = await NewContractorAsync(org);
        var active = await ActiveContractAsync(org, projectId, contractor.Id);

        // نهاية قبل البداية: مرفوضة؛ ثم بداية ومدة بلا نهاية تُقترح ولا تُكتب
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Put<ContractDetailDto>($"{Base}/Update/{active.Id}", new
        {
            projectId, method = 1, contractorId = contractor.Id, number = active.OfficialNumber, contractDate = active.ContractDate, startDate = active.StartDate, durationDays = active.DurationDays,
            value = active.Value, currency = "SYP", warrantyStart = DateTime.Today, warrantyEnd = DateTime.Today.AddDays(-1), rowVersion = active.RowVersion
        })).Status);

        var withStart = (await org.Office.Put<ContractDetailDto>($"{Base}/Update/{active.Id}", new
        {
            projectId, method = 1, contractorId = contractor.Id, number = active.OfficialNumber, contractDate = active.ContractDate, startDate = active.StartDate, durationDays = active.DurationDays,
            value = active.Value, currency = "SYP", warrantyMonths = 12, warrantyStart = DateTime.Today, rowVersion = active.RowVersion
        })).Ok();
        Assert.Null(withStart.WarrantyEnd);
        Assert.Equal(DateTime.Today.AddMonths(12).AddDays(-1), withStart.SuggestedWarrantyEnd);

        // نهاية بعد 10 أيام: يظهر «ينتهي قريباً» ويُفلتَر
        var ending = (await org.Office.Put<ContractDetailDto>($"{Base}/Update/{withStart.Id}", new
        {
            projectId, method = 1, contractorId = contractor.Id, number = active.OfficialNumber, contractDate = active.ContractDate, startDate = active.StartDate, durationDays = active.DurationDays,
            value = active.Value, currency = "SYP", warrantyMonths = 12, warrantyStart = DateTime.Today.AddMonths(-12).AddDays(10), warrantyEnd = DateTime.Today.AddDays(10), rowVersion = withStart.RowVersion
        })).Ok();
        Assert.True(ending.WarrantyEndingSoon);
        Assert.Equal(10, ending.WarrantyDaysLeft);
        var soon = (await org.Office.Get<PagedResultDto<ContractListItemDto>>($"{Base}/Search?projectId={projectId}&warrantyEndingOnly=true")).Ok();
        Assert.Contains(soon.Items, c => c.Id == ending.Id);
    }
}
