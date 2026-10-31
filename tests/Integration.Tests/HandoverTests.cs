using System.Net;
using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Contracts;
using Application.Features.Handover;
using Application.Interfaces;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// الاستلام والتسليم على القاعدة الحقيقية: طلب واحد غير منتهٍ لكل نوع، لجنة، كشف ونتيجة («بملاحظات» تشترط ملاحظة مسجَّلة)، متابعة الملاحظات
/// (من عالج لا يتحقق)، النهائي بلا ملاحظات يشترط إغلاق ما في المشروع، تنبيه إغلاق المشروع، التذكيرات (مرة لكل موعد)، وتقرير العقود.
/// </summary>
[Collection(ApiCollection.Name)]
public class HandoverTests(ApiFixture api)
{
    private const string Base = "/api/Handovers";

    private sealed record Org(ApiClient Office, int OfficeUserId, ApiClient Engineer, int EngineerId, ApiClient Stranger);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    private async Task<(ApiClient Client, int Id)> UserAsync(string label, int roleId)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("استلام " + label, email, roleId, api.BranchId, api.DepartmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return (api.WithToken(login.Token, id), id);
    }

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var officeRole = await api.CreateRoleAsync("قسم الإشراف " + suffix,
            ["ViewProjects", "ViewBranchProjects", "ViewHandovers", "ManageHandover", "FollowHandoverNotes", "ViewContracts", "CreateContract", "EditContract",
             "ViewContractors", "CreateContractor", "ExportContractsReport", "ViewNotifications"], all);
        var engineerRole = await api.CreateRoleAsync("مهندس موقع " + suffix, ["ViewProjects", "RequestHandover", "FollowHandoverNotes", "ViewNotifications"], all);
        var strangerRole = await api.CreateRoleAsync("غريب استلام " + suffix, ["ViewProjects", "ViewHandovers"], all);

        var office = await UserAsync("office", officeRole);
        var engineer = await UserAsync("eng", engineerRole);
        var stranger = await UserAsync("str", strangerRole);
        return new Org(office.Client, office.Id, engineer.Client, engineer.Id, stranger.Client);
    }

    /// <summary>مشروع بمرحلة التنفيذ (فما بعد)، والمهندس مشارك فيه</summary>
    private async Task<int> NewProjectAsync(Org org, int stage = 3)
    {
        var project = (await api.Preparer.Post<ProjectResponseDto>("/api/Projects/Create", new ProjectRequestDto
        {
            Name = $"مشروع استلام {Guid.NewGuid():N}"[..28], Beneficiary = "جهة اختبار", DepartmentId = api.DepartmentId
        })).Ok();
        if (stage != 1)
            (await api.Preparer.Put<ProjectResponseDto>($"/api/Projects/Update/{project.Id}", new ProjectRequestDto
            {
                Name = project.Name, Beneficiary = "جهة اختبار", DepartmentId = api.DepartmentId, Stage = stage, RowVersion = project.RowVersion
            })).Ok();
        (await api.Preparer.Post<JsonElement>($"/api/Projects/Members/{project.Id}", new { userId = org.EngineerId, title = "مهندس موقع" })).Ok();
        return project.Id;
    }

    private static object Committee(Org org, byte[]? rowVersion = null) => new
    {
        formedAt = DateTime.Today, decisionNumber = "قرار استلام", rowVersion,
        members = new object[] { new { userId = org.OfficeUserId, capacity = "رئيس اللجنة", isChair = true }, new { externalName = "ممثل المقاول", capacity = "ممثل" } }
    };

    /// <summary>طلب ← لجنة؛ يعيد المحضر وقد صار «لجنة مشكَّلة»</summary>
    private async Task<HandoverDetailDto> FormedAsync(Org org, int projectId, int kind)
    {
        var requested = (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId, kind, note = "انتهت الأعمال" })).Ok();
        return (await org.Office.Post<HandoverDetailDto>($"{Base}/Committee/{requested.Id}", Committee(org, requested.RowVersion))).Ok();
    }

    // ───────── الطلب واللجنة ─────────

    [Fact]
    public async Task A_request_needs_an_executing_project_and_only_one_unfinished_request_per_kind_exists()
    {
        var org = await OrgAsync();

        // مشروع في مرحلة الدراسة: لا استلام
        var early = await NewProjectAsync(org, stage: 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId = early, kind = 1 })).Status);

        var projectId = await NewProjectAsync(org);
        var request = (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId, kind = 1, note = "طلب" })).Ok();
        Assert.StartsWith("HND-", request.Number);
        Assert.Equal(1, request.Status);
        Assert.False(request.CanCancel);                               // المهندس لا يملك إدارة الاستلام
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId, kind = 1 })).Status);        // أولي ثانٍ
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId, kind = 7 })).Status);        // نوع غير معروف
        (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId, kind = 2 })).Ok();                                                   // النهائي مستقل

        // اللجنة لمن يملك إدارة الاستلام: رئيس واحد، وبلا أعضاء مرفوضة
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Committee/{request.Id}", Committee(org))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<HandoverDetailDto>($"{Base}/Committee/{request.Id}",
            new { formedAt = DateTime.Today, members = Array.Empty<object>(), rowVersion = request.RowVersion })).Status);
        var formed = (await org.Office.Post<HandoverDetailDto>($"{Base}/Committee/{request.Id}", Committee(org, request.RowVersion))).Ok();
        Assert.Equal(2, formed.Status);
        Assert.NotNull(formed.Committee);
        Assert.Equal(2, formed.Committee!.Members.Count);
        Assert.Single(formed.Committee.Members, m => m.IsChair);

        // لجنة ثانية للطلب نفسه مرفوضة، وتعديلها قبل النتيجة يستبدل الأعضاء
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<HandoverDetailDto>($"{Base}/Committee/{request.Id}", Committee(org, formed.RowVersion))).Status);
        var edited = (await org.Office.Put<HandoverDetailDto>($"{Base}/Committee/{request.Id}", new
        {
            formedAt = DateTime.Today, decisionNumber = "قرار معدَّل", rowVersion = formed.Committee.RowVersion,
            members = new object[] { new { userId = org.EngineerId, capacity = "رئيس", isChair = true }, new { userId = org.OfficeUserId, capacity = "عضو" } }
        })).Ok();
        Assert.Equal(org.EngineerId, edited.Committee!.Members.Single(m => m.IsChair).UserId);

        // إلغاء الطلب بدل حذفه، ثم يُسمح بطلب جديد من النوع نفسه
        var cancelled = (await org.Office.Post<HandoverDetailDto>($"{Base}/Cancel/{request.Id}", new { reason = "تقديم خاطئ", rowVersion = edited.RowVersion })).Ok();
        Assert.Equal(4, cancelled.Status);
        (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId, kind = 1 })).Ok();
    }

    // ───────── النتيجة والملاحظات ─────────

    [Fact]
    public async Task A_with_notes_result_needs_a_recorded_note_and_the_fixer_never_verifies_their_own_fix()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var formed = await FormedAsync(org, projectId, kind: 1);

        // بلا ملاحظات: «بملاحظات» مرفوضة؛ بعد تسجيل ملاحظة تصير «بلا ملاحظات» مرفوضة
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<HandoverDetailDto>($"{Base}/Conclude/{formed.Id}",
            new { result = 2, inspectionDate = DateTime.Today, rowVersion = formed.RowVersion })).Status);
        var withNote = (await org.Office.Post<HandoverDetailDto>($"{Base}/Notes/{formed.Id}",
            new { description = "تشققات في الجدار الشمالي", responsibleName = "المقاول", dueDate = DateTime.Today.AddDays(10) })).Ok();
        var note = Assert.Single(withNote.Notes);
        Assert.Equal("المقاول", note.ResponsibleName);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<HandoverDetailDto>($"{Base}/Conclude/{formed.Id}",
            new { result = 1, inspectionDate = DateTime.Today, rowVersion = withNote.RowVersion })).Status);
        // تاريخ كشف في المستقبل مرفوض
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<HandoverDetailDto>($"{Base}/Conclude/{formed.Id}",
            new { result = 2, inspectionDate = DateTime.Today.AddDays(1), rowVersion = withNote.RowVersion })).Status);

        var concluded = (await org.Office.Post<HandoverDetailDto>($"{Base}/Conclude/{formed.Id}",
            new { result = 2, inspectionDate = DateTime.Today, resultNote = "ملاحظات على الواجهة", rowVersion = withNote.RowVersion })).Ok();
        Assert.Equal(3, concluded.Status);
        Assert.Equal(2, concluded.Result);
        Assert.Equal(1, concluded.OpenNotes);

        // المعالجة: غير الموظف المسؤول أيضاً يعالج (من له الصلاحية)، ومن عالج لا يتحقق
        var fixing = concluded.Notes.Single();
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Note/{fixing.Id}/Fix", new { fixNote = " ", rowVersion = fixing.RowVersion })).Status);
        var fixedUp = (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Note/{fixing.Id}/Fix", new { fixNote = "رُمّمت التشققات", rowVersion = fixing.RowVersion })).Ok();
        var fixedNote = fixedUp.Notes.Single();
        Assert.Equal(2, fixedNote.Status);
        Assert.False(fixedNote.CanVerify);                             // من عالج لا يتحقق
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Note/{fixedNote.Id}/Verify", new { rowVersion = fixedNote.RowVersion })).Status);

        // رفض المعالجة يشترط سبباً ويعيد الملاحظة مفتوحة؛ ثم معالجة جديدة وتحقق من غير المعالِج
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<HandoverDetailDto>($"{Base}/Note/{fixedNote.Id}/Reject", new { note = " ", rowVersion = fixedNote.RowVersion })).Status);
        var reopened = (await org.Office.Post<HandoverDetailDto>($"{Base}/Note/{fixedNote.Id}/Reject", new { note = "لم يُعالَج الشق الأيمن", rowVersion = fixedNote.RowVersion })).Ok().Notes.Single();
        Assert.Equal(1, reopened.Status);
        var again = (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Note/{reopened.Id}/Fix", new { fixNote = "عولج الشق الأيمن", rowVersion = reopened.RowVersion })).Ok().Notes.Single();
        var verified = (await org.Office.Post<HandoverDetailDto>($"{Base}/Note/{again.Id}/Verify", new { note = "سليم", rowVersion = again.RowVersion })).Ok();
        Assert.Equal(3, verified.Notes.Single().Status);
        Assert.Equal(0, verified.OpenNotes);

        // المحضر المنتهي لا تُحذف ملاحظاته ولا تُضاف إليه بعد «بلا ملاحظات»
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Delete($"{Base}/Note/{again.Id}")).Status);
    }

    [Fact]
    public async Task A_final_without_notes_waits_for_every_open_note_in_the_project_and_is_recorded_once()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);

        // أولي بملاحظة مفتوحة
        var initial = await FormedAsync(org, projectId, kind: 1);
        initial = (await org.Office.Post<HandoverDetailDto>($"{Base}/Notes/{initial.Id}", new { description = "ملاحظة سباكة" })).Ok();
        initial = (await org.Office.Post<HandoverDetailDto>($"{Base}/Conclude/{initial.Id}", new { result = 2, inspectionDate = DateTime.Today, rowVersion = initial.RowVersion })).Ok();

        // الإنذار قبل الإغلاق يعدّد ما لم يكتمل (تنبيه لا منع)
        var warnings = (await org.Office.Get<ClosureWarningsDto>($"{Base}/ClosureWarnings/{projectId}")).Ok().Warnings;
        Assert.Contains(warnings, w => w.Contains("1 ملاحظة"));

        // النهائي بلا ملاحظات مرفوض ما دامت ملاحظة الأولي مفتوحة
        var final = await FormedAsync(org, projectId, kind: 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Office.Post<HandoverDetailDto>($"{Base}/Conclude/{final.Id}",
            new { result = 1, inspectionDate = DateTime.Today, rowVersion = final.RowVersion })).Status);

        // تُعالَج وتُغلق (معالجة المهندس وتحقق المكتب) ثم يُقبل
        var note = initial.Notes.Single();
        var fixedUp = (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Note/{note.Id}/Fix", new { fixNote = "أُصلحت", rowVersion = note.RowVersion })).Ok().Notes.Single();
        (await org.Office.Post<HandoverDetailDto>($"{Base}/Note/{fixedUp.Id}/Verify", new { rowVersion = fixedUp.RowVersion })).Ok();

        var fresh = (await org.Office.Get<HandoverDetailDto>($"{Base}/Get/{final.Id}")).Ok();
        var done = (await org.Office.Post<HandoverDetailDto>($"{Base}/Conclude/{final.Id}",
            new { result = 1, inspectionDate = DateTime.Today, rowVersion = fresh.RowVersion })).Ok();
        Assert.Equal(1, done.Result);

        // سُجّل نهائي بلا ملاحظات: لا طلب نهائي ثانٍ، ولم يعد إنذار الاستلام النهائي
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId, kind = 2 })).Status);
        var after = (await org.Office.Get<ClosureWarningsDto>($"{Base}/ClosureWarnings/{projectId}")).Ok().Warnings;
        Assert.DoesNotContain(after, w => w.Contains("استلام نهائي بلا ملاحظات"));
        Assert.DoesNotContain(after, w => w.Contains("ملاحظة استلام لم تُغلق"));
    }

    // ───────── الحدود ─────────

    [Fact]
    public async Task Someone_outside_the_project_does_not_see_its_handovers_and_the_list_respects_the_same_boundary()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var request = (await org.Engineer.Post<HandoverDetailDto>($"{Base}/Request", new { projectId, kind = 1 })).Ok();

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Stranger.Get<HandoverDetailDto>($"{Base}/Get/{request.Id}")).Status);
        Assert.DoesNotContain((await org.Stranger.Get<PagedResultDto<HandoverListItemDto>>($"{Base}/Search?projectId={projectId}")).Ok().Items, h => h.Id == request.Id);
        Assert.Contains((await org.Office.Get<PagedResultDto<HandoverListItemDto>>($"{Base}/Search?projectId={projectId}&kind=1&status=1")).Ok().Items, h => h.Id == request.Id);
        Assert.DoesNotContain((await org.Office.Get<PagedResultDto<HandoverListItemDto>>($"{Base}/Search?projectId={projectId}&kind=2")).Ok().Items, h => h.Id == request.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Outsider.Get<PagedResultDto<HandoverListItemDto>>($"{Base}/Search")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Office.Get<HandoverDetailDto>($"{Base}/Get/999999")).Status);
    }

    // ───────── التذكيرات والتقرير ─────────

    [Fact]
    public async Task An_overdue_note_is_notified_once_per_due_date_and_changing_the_date_allows_a_new_notice()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var formed = await FormedAsync(org, projectId, kind: 1);
        var noted = (await org.Office.Post<HandoverDetailDto>($"{Base}/Notes/{formed.Id}",
            new { description = "ملاحظة متأخرة", responsibleUserId = org.EngineerId, dueDate = DateTime.Today.AddDays(-3) })).Ok();
        Assert.True(noted.HasOverdueNote);
        Assert.True(noted.Notes.Single().IsOverdue);

        async Task<int> RunAsync()
        {
            await using var scope = api.Services.CreateAsyncScope();
            return await HandoverReminders.RunAsync(scope.ServiceProvider.GetRequiredService<IHandoverService>(), scope.ServiceProvider.GetRequiredService<IUserPermissionService>(),
                scope.ServiceProvider.GetRequiredService<INotificationService>(), DateTime.Now);
        }

        Assert.True(await RunAsync() >= 1);
        Assert.Equal(0, await RunAsync());                                // مرة لكل موعد

        // تغيير الموعد إلى موعد متأخر آخر يسمح بتنبيه جديد
        // التذكير يحدّث الملاحظة مباشرة (تتغير نسخة الصف): أعد قراءتها قبل التعديل
        var note = (await org.Office.Get<HandoverDetailDto>($"{Base}/Get/{formed.Id}")).Ok().Notes.Single();
        (await org.Office.Put<HandoverDetailDto>($"{Base}/Note/{note.Id}", new { description = "ملاحظة متأخرة", responsibleUserId = org.EngineerId, dueDate = DateTime.Today.AddDays(-2), rowVersion = note.RowVersion })).Ok();
        Assert.True(await RunAsync() >= 1);

        // لم تتغير حالة الملاحظة بالتنبيه
        Assert.Equal(1, (await org.Office.Get<HandoverDetailDto>($"{Base}/Get/{formed.Id}")).Ok().Notes.Single().Status);
    }

    [Fact]
    public async Task A_warranty_ending_soon_is_notified_once_per_end_and_the_contracts_report_lists_the_contract()
    {
        var org = await OrgAsync();
        var projectId = await NewProjectAsync(org);
        var contractor = (await org.Office.Post<ContractorResponseDto>("/api/Contractors/Create", new { name = $"مقاول {Guid.NewGuid():N}"[..14], isActive = true })).Ok();
        var number = $"CT-{Guid.NewGuid():N}"[..11];

        var contract = (await org.Office.Post<ContractDetailDto>("/api/Contracts/Create", new
        {
            projectId, method = 1, contractorId = contractor.Id, number, contractDate = DateTime.Today.AddDays(-3), value = 500_000m, currency = "SYP",
            warrantyMonths = 12, warrantyStart = DateTime.Today.AddMonths(-12).AddDays(10), warrantyEnd = DateTime.Today.AddDays(10)
        })).Ok();
        contract = (await org.Office.Post<ContractDetailDto>($"/api/Contracts/Status/{contract.Id}", new { status = 2, rowVersion = contract.RowVersion })).Ok();
        Assert.True(contract.WarrantyEndingSoon);

        async Task<int> RunAsync()
        {
            await using var scope = api.Services.CreateAsyncScope();
            return await ContractReminders.RunAsync(scope.ServiceProvider.GetRequiredService<IContractService>(), scope.ServiceProvider.GetRequiredService<IUserPermissionService>(),
                scope.ServiceProvider.GetRequiredService<INotificationService>(), DateTime.Now);
        }
        Assert.True(await RunAsync() >= 1);
        Assert.Equal(0, await RunAsync());

        // التقرير: يسرد العقد برقمه الرسمي، ومن لا يملك صلاحية التقارير لا يسحبه
        var preview = await org.Office.Post<JsonElement>("/api/Reports/Preview/contracts", new { filters = new Dictionary<string, string>(), page = 1 });
        Assert.Equal(HttpStatusCode.OK, preview.Status);
        Assert.Contains(number, preview.Raw);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Engineer.Post<JsonElement>("/api/Reports/Preview/contracts", new { filters = new Dictionary<string, string>() })).Status);
        // تقرير التقييمات يشترط «عرض التقييمات» فوق صلاحية التقارير
        Assert.NotEqual(HttpStatusCode.OK, (await org.Office.Post<JsonElement>("/api/Reports/Preview/contractor-evaluations", new { filters = new Dictionary<string, string>() })).Status);
    }
}
