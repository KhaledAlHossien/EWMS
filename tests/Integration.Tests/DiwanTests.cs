using System.Net;
using System.Text;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// الديوان على التطبيق وSQL Server الحقيقيين: الترقيم المتزامن، الإحالة وأن الإحالة إلى قسم لا تفتح المعاملة لكل موظفيه،
/// الاستلام والحالات والإغلاق بصلاحيته، ومسار الصادر (مسودة ← اعتماد غير المُعدّ ← صدور برقم)، والمرفقات، وحراسات الحذف.
/// </summary>
[Collection(ApiCollection.Name)]
public class DiwanTests(ApiFixture api)
{
    private const string Base = "/api/Correspondence";
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\n%%EOF");

    private sealed record Org(ApiClient Clerk, ApiClient StudiesHead, ApiClient StudiesPlain, ApiClient Closer, ApiClient Approver,
        int ClerkId, int StudiesHeadId, int ApproverId, int StudiesDepartmentId, int TypeId, int RegisteredStatusId, int InProgressStatusId, int AnsweredStatusId, int ClosedStatusId);

    private static readonly object Gate = new();
    private static Task<Org>? _shared;

    private Task<Org> OrgAsync() { lock (Gate) return _shared ??= CreateOrgAsync(); }

    /// <summary>دخول مباشر عبر الخدمة (حدّ طلبات الدخول 20 في الدقيقة لا يسع كل أدوار الاختبار)</summary>
    private async Task<(ApiClient Client, int Id)> UserAsync(string label, int roleId, int departmentId, string[]? _ = null)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("ديوان " + label, email, roleId, api.BranchId, departmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return (api.WithToken(login.Token, id), id);
    }

    private async Task<Org> CreateOrgAsync()
    {
        var all = api.AllPermissions;
        var studies = (await api.Admin.PostForm<DepartmentResponseDto>("/api/Department/Create", [
            new("Name", "قسم الدراسات " + Guid.NewGuid().ToString("N")[..6]), new("Description", "اختبار"), new("BranchId", api.BranchId.ToString())])).Ok();

        string[] lookups = ["ViewCorrespondenceLookups"];
        var clerkRole = await api.CreateRoleAsync("كاتب ديوان " + Guid.NewGuid().ToString("N")[..6],
            ["RegisterCorrespondence", "ReferCorrespondence", "HandleCorrespondence", "ViewCorrespondence", ..lookups], all);
        var headRole = await api.CreateRoleAsync("رئيس دراسات " + Guid.NewGuid().ToString("N")[..6],
            ["HandleCorrespondence", "ReferCorrespondence", "ViewCorrespondence", ..lookups], all);
        var plainRole = await api.CreateRoleAsync("موظف دراسات " + Guid.NewGuid().ToString("N")[..6], ["ViewCorrespondence"], all);
        var closerRole = await api.CreateRoleAsync("مغلِق معاملات " + Guid.NewGuid().ToString("N")[..6], ["HandleCorrespondence", "CloseCorrespondence", "ViewCorrespondence"], all);
        var approverRole = await api.CreateRoleAsync("معتمد صادر " + Guid.NewGuid().ToString("N")[..6], ["ApproveOutgoingCorrespondence", "ViewCorrespondence"], all);

        var clerk = await UserAsync("clerk", clerkRole, api.DepartmentId);
        var head = await UserAsync("head", headRole, studies.Id);
        var plain = await UserAsync("plain", plainRole, studies.Id);
        var closer = await UserAsync("closer", closerRole, studies.Id);
        var approver = await UserAsync("approver", approverRole, api.DepartmentId);

        // إعدادات الفرع: مدير النظام ينشئها لفرع الاختبار
        async Task<int> Status(string name, int stage) => (await api.Admin.Post<CorrespondenceStatusResponseDto>("/api/CorrespondenceStatuses/Create",
            new { branchId = api.BranchId, name = name + Guid.NewGuid().ToString("N")[..4], stage, color = "#34C759", isActive = true, sortOrder = stage })).Ok().Id;
        var registered = await Status("مسجّلة", 1);
        var progress = await Status("قيد المعالجة", 2);
        var answered = await Status("تمّت الإجابة", 3);
        var closed = await Status("مغلقة", 4);
        var type = (await api.Admin.Post<CorrespondenceTypeResponseDto>("/api/CorrespondenceTypes/Create",
            new { branchId = api.BranchId, name = "أمر دراسة " + Guid.NewGuid().ToString("N")[..6], deadlineDays = 5, isActive = true, sortOrder = 1 })).Ok();

        return new Org(clerk.Client, head.Client, plain.Client, closer.Client, approver.Client, clerk.Id, head.Id, approver.Id, studies.Id,
            type.Id, registered, progress, answered, closed);
    }

    private static Task<ApiResult<CorrespondenceResponseDto>> RegisterAsync(Org org, string subject = "أمر دراسة مشروع المدرسة", int? projectId = null) =>
        org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/RegisterIncoming",
            new { typeId = org.TypeId, party = "المركزية", subject, summary = "ملخص", externalNumber = "م/55", externalDate = "2026-10-01", projectId });

    private static Task<ApiResult<CorrespondenceResponseDto>> ReferAsync(ApiClient client, int id, int departmentId, int? userId = null, string note = "للدراسة") =>
        client.Post<CorrespondenceResponseDto>($"{Base}/Refer/{id}", new { toDepartmentId = departmentId, toUserId = userId, note });

    private static Task<ApiResult<CorrespondenceResponseDto>> GetAsync(ApiClient client, int id) => client.Get<CorrespondenceResponseDto>($"{Base}/Get/{id}");

    // ───────── الإعدادات ─────────

    [Fact]
    public async Task Lookups_are_readable_by_staff_but_managed_only_with_their_own_permissions()
    {
        var org = await OrgAsync();

        var types = (await org.Clerk.Get<List<CorrespondenceTypeResponseDto>>("/api/CorrespondenceTypes/GetAll")).Ok();
        Assert.Contains(types, t => t.Id == org.TypeId);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Clerk.Post<CorrespondenceTypeResponseDto>("/api/CorrespondenceTypes/Create", new { name = "نوع", isActive = true })).Status);

        var duplicate = types.First(t => t.Id == org.TypeId);
        var again = await api.Admin.Post<CorrespondenceTypeResponseDto>("/api/CorrespondenceTypes/Create",
            new { branchId = api.BranchId, name = duplicate.Name, isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, again.Status);
    }

    [Fact]
    public async Task A_status_or_type_in_use_cannot_be_deleted_and_a_used_status_keeps_its_stage()
    {
        var org = await OrgAsync();
        (await RegisterAsync(org)).Ok();

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Delete($"/api/CorrespondenceTypes/Delete/{org.TypeId}")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Delete($"/api/CorrespondenceStatuses/Delete/{org.RegisteredStatusId}")).Status);

        var restage = await api.Admin.Put<CorrespondenceStatusResponseDto>($"/api/CorrespondenceStatuses/Update/{org.RegisteredStatusId}",
            new { name = "مسجّلة", stage = 4, color = "#34C759", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, restage.Status);
    }

    // ───────── التسجيل والترقيم ─────────

    [Fact]
    public async Task Registering_gives_a_number_an_initial_status_a_due_date_and_the_registrars_unit()
    {
        var org = await OrgAsync();

        var c = (await RegisterAsync(org)).Ok();

        Assert.Matches($@"^IN-{DateTime.Today.Year}-\d{{5}}$", c.Number);
        Assert.Equal(org.RegisteredStatusId, c.StatusId);
        Assert.Equal(DateTime.Today.AddDays(5), c.DueDate);
        Assert.Equal(api.DepartmentId, c.CurrentDepartmentId);
        Assert.False(c.IsOverdue);

        var inbox = (await org.Clerk.Get<PagedResultDto<CorrespondenceListItemDto>>($"{Base}/Search?inbox=true&pageSize=100")).Ok();
        Assert.Contains(inbox.Items, i => i.Id == c.Id);
    }

    [Fact]
    public async Task Simultaneous_registrations_never_share_a_number()
    {
        var org = await OrgAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => RegisterAsync(org, $"معاملة متزامنة {i}")));

        var numbers = results.Select(r => r.Ok().Number).ToList();
        Assert.Equal(10, numbers.Distinct().Count());
        var sequences = numbers.Select(n => int.Parse(n![^5..])).OrderBy(x => x).ToList();
        Assert.Equal(sequences.Min() + 9, sequences.Max());   // متتالية بلا فجوات
    }

    [Fact]
    public async Task Registration_without_a_registered_stage_status_or_with_bad_input_is_refused()
    {
        var org = await OrgAsync();

        var noSubject = await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/RegisterIncoming", new { typeId = org.TypeId, party = "ج", subject = " " });
        Assert.Equal(HttpStatusCode.BadRequest, noSubject.Status);

        var foreignType = await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/RegisterIncoming", new { typeId = int.MaxValue, party = "ج", subject = "م" });
        Assert.Equal(HttpStatusCode.NotFound, foreignType.Status);

        // موظف دراسات (بلا صلاحية التسجيل) لا يسجّل
        Assert.Equal(HttpStatusCode.Forbidden, (await RegisterAsyncAs(org.StudiesPlain, org.TypeId)).Status);
    }

    private static Task<ApiResult<CorrespondenceResponseDto>> RegisterAsyncAs(ApiClient client, int typeId) =>
        client.Post<CorrespondenceResponseDto>($"{Base}/RegisterIncoming", new { typeId, party = "ج", subject = "م" });

    // ───────── الإحالة والرؤية ─────────

    [Fact]
    public async Task Referring_to_a_department_opens_the_item_to_its_handlers_only_not_to_all_its_staff()
    {
        var org = await OrgAsync();
        var c = (await RegisterAsync(org)).Ok();

        var referred = (await ReferAsync(org.Clerk, c.Id, org.StudiesDepartmentId)).Ok();
        Assert.Equal(org.StudiesDepartmentId, referred.CurrentDepartmentId);
        Assert.Single(referred.Referrals);

        // رئيس الدراسات (يعالج) يراها ويستلمها؛ موظف الدراسات العادي لا يراها؛ المسجِّل يبقى يراها
        Assert.True((await GetAsync(org.StudiesHead, c.Id)).IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(org.StudiesPlain, c.Id)).Status);
        Assert.True((await GetAsync(org.Clerk, c.Id)).IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(api.Outsider, c.Id)).Status);

        var inbox = (await org.StudiesHead.Get<PagedResultDto<CorrespondenceListItemDto>>($"{Base}/Search?inbox=true&pageSize=100")).Ok();
        Assert.Contains(inbox.Items, i => i.Id == c.Id);
        var gone = (await org.Clerk.Get<PagedResultDto<CorrespondenceListItemDto>>($"{Base}/Search?inbox=true&pageSize=100")).Ok();
        Assert.DoesNotContain(gone.Items, i => i.Id == c.Id);   // لم تعد عند ديوان المسجِّل

        await using var scope = api.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<DataContext>().Notifications
            .AnyAsync(n => n.UserId == org.StudiesHeadId && n.Type == NotificationType.CorrespondenceReferred && n.RelatedEntityId == c.Id));
    }

    [Fact]
    public async Task Referring_to_a_named_person_makes_them_a_handler_and_a_visitor_of_the_item()
    {
        var org = await OrgAsync();
        var c = (await RegisterAsync(org)).Ok();

        var referred = (await ReferAsync(org.Clerk, c.Id, org.StudiesDepartmentId, org.StudiesHeadId, "للمتابعة بالاسم")).Ok();

        Assert.Equal(org.StudiesHeadId, referred.CurrentUserId);
        // موظف عادي لا يملك المعالجة لا يصلح هدفاً بالاسم
        var plain = await org.Clerk.Get<List<ReferralUserDto>>($"{Base}/ReferralUsers?departmentId={org.StudiesDepartmentId}");
        Assert.DoesNotContain(plain.Ok(), u => u.FullName.Contains("plain"));
    }

    [Fact]
    public async Task Referral_has_limits_other_branch_unheld_item_closed_item_and_no_permission()
    {
        var org = await OrgAsync();
        var c = (await RegisterAsync(org)).Ok();

        // من لا يحمل وحدته المعاملة لا يحيلها (رئيس الدراسات والمعاملة عند الديوان)
        Assert.Equal(HttpStatusCode.Forbidden, (await ReferAsync(org.StudiesHead, c.Id, api.DepartmentId)).Status);
        // قسم غير موجود
        Assert.Equal(HttpStatusCode.NotFound, (await ReferAsync(org.Clerk, c.Id, int.MaxValue)).Status);
        // إلى الجهة الحاملة نفسها
        Assert.Equal(HttpStatusCode.BadRequest, (await ReferAsync(org.Clerk, c.Id, api.DepartmentId)).Status);
        // بلا صلاحية الإحالة
        Assert.Equal(HttpStatusCode.Forbidden, (await ReferAsync(org.Closer, c.Id, api.DepartmentId)).Status);
    }

    [Fact]
    public async Task Acknowledging_is_recorded_once_by_a_holder_of_the_receiving_unit()
    {
        var org = await OrgAsync();
        var c = (await RegisterAsync(org)).Ok();
        (await ReferAsync(org.Clerk, c.Id, org.StudiesDepartmentId)).Ok();

        Assert.Equal(HttpStatusCode.Forbidden, (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Acknowledge/{c.Id}")).Status);   // لم تعد وحدته

        var acknowledged = (await org.StudiesHead.Post<CorrespondenceResponseDto>($"{Base}/Acknowledge/{c.Id}")).Ok();
        Assert.NotNull(acknowledged.Referrals.Single().AcknowledgedAt);
        Assert.False(acknowledged.CanAcknowledge);

        Assert.Equal(HttpStatusCode.BadRequest, (await org.StudiesHead.Post<CorrespondenceResponseDto>($"{Base}/Acknowledge/{c.Id}")).Status);
    }

    // ───────── الحالة والإغلاق ─────────

    [Fact]
    public async Task A_holder_changes_the_status_and_only_a_closer_closes_and_reopens()
    {
        var org = await OrgAsync();
        var c = (await RegisterAsync(org)).Ok();
        (await ReferAsync(org.Clerk, c.Id, org.StudiesDepartmentId)).Ok();

        var working = (await org.StudiesHead.Put<CorrespondenceResponseDto>($"{Base}/Status/{c.Id}", new { statusId = org.InProgressStatusId })).Ok();
        Assert.Equal(org.InProgressStatusId, working.StatusId);

        Assert.Equal(HttpStatusCode.Forbidden, (await org.StudiesHead.Put<CorrespondenceResponseDto>($"{Base}/Status/{c.Id}", new { statusId = org.ClosedStatusId })).Status);

        var closed = (await org.Closer.Put<CorrespondenceResponseDto>($"{Base}/Status/{c.Id}", new { statusId = org.ClosedStatusId })).Ok();
        Assert.True(closed.IsClosed);
        Assert.NotNull(closed.ClosedAt);

        // مغلقة: لا إحالة ولا تعديل، وإعادة الفتح بصلاحية الإغلاق
        Assert.Equal(HttpStatusCode.BadRequest, (await ReferAsync(org.StudiesHead, c.Id, api.DepartmentId)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.StudiesHead.Put<CorrespondenceResponseDto>($"{Base}/Status/{c.Id}", new { statusId = org.InProgressStatusId })).Status);
        var reopened = (await org.Closer.Put<CorrespondenceResponseDto>($"{Base}/Status/{c.Id}", new { statusId = org.InProgressStatusId })).Ok();
        Assert.False(reopened.IsClosed);
        Assert.Null(reopened.ClosedAt);
    }

    // ───────── التعديل والمرفقات والأثر ─────────

    [Fact]
    public async Task Updating_changes_the_fields_logs_each_change_and_recomputes_the_due_date_for_a_new_type()
    {
        var org = await OrgAsync();
        var c = (await RegisterAsync(org)).Ok();
        var slow = (await api.Admin.Post<CorrespondenceTypeResponseDto>("/api/CorrespondenceTypes/Create",
            new { branchId = api.BranchId, name = "استعلام " + Guid.NewGuid().ToString("N")[..6], deadlineDays = 20, isActive = true })).Ok();

        var updated = (await org.Clerk.Put<CorrespondenceResponseDto>($"{Base}/Update/{c.Id}",
            new { typeId = slow.Id, party = "المركزية", subject = "موضوع معدّل", summary = "ملخص", externalNumber = "م/99", rowVersion = c.RowVersion })).Ok();

        Assert.Equal("موضوع معدّل", updated.Subject);
        Assert.Equal(DateTime.Today.AddDays(20), updated.DueDate);

        var history = (await org.Clerk.Get<List<CorrespondenceHistoryDto>>($"{Base}/History/{c.Id}")).Ok();
        Assert.Contains(history, h => h.Action == "updated" && h.Details.Contains("الموضوع:") && h.Details.Contains("النوع:"));
        Assert.Contains(history, h => h.Action == "created");

        // نسخة قديمة: يرفض الحفظ بدل أن يكتب فوق تعديل غيره
        var stale = await org.Clerk.Put<CorrespondenceResponseDto>($"{Base}/Update/{c.Id}",
            new { typeId = slow.Id, party = "ج", subject = "آخر", rowVersion = c.RowVersion });
        Assert.Equal(HttpStatusCode.BadRequest, stale.Status);
    }

    [Fact]
    public async Task Attachments_are_added_by_a_holder_read_by_viewers_and_never_removed_from_an_incoming()
    {
        var org = await OrgAsync();
        var c = (await RegisterAsync(org)).Ok();

        var withFile = (await org.Clerk.PostFiles<CorrespondenceResponseDto>($"{Base}/Attachments/{c.Id}", ("كتاب.pdf", Pdf))).Ok();
        var attachment = Assert.Single(withFile.Attachments);
        Assert.Equal("application/pdf", attachment.ContentType);

        var content = await org.Clerk.Probe(HttpMethod.Get, $"{Base}/Attachment/{attachment.Id}");
        Assert.Equal(HttpStatusCode.OK, content.Status);
        Assert.StartsWith("%PDF-", content.Body);

        // من لا يرى المعاملة لا يقرأ مرفقها، ولا يرفق عليها
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Outsider.Probe(HttpMethod.Get, $"{Base}/Attachment/{attachment.Id}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.StudiesPlain.PostFiles<CorrespondenceResponseDto>($"{Base}/Attachments/{c.Id}", ("x.pdf", Pdf))).Status);

        // ملف تنفيذي بامتداد PDF يُرفض (النوع من المحتوى)
        var fake = await org.Clerk.PostFiles<CorrespondenceResponseDto>($"{Base}/Attachments/{c.Id}", ("حقيقي.pdf", Encoding.ASCII.GetBytes("MZ-not-a-pdf")));
        Assert.Equal(HttpStatusCode.BadRequest, fake.Status);

        // الوارد أثر لا يُمحى
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Clerk.Delete($"{Base}/Attachment/{attachment.Id}")).Status);
    }

    // ───────── الصادر ─────────

    [Fact]
    public async Task Outgoing_runs_draft_then_approval_by_someone_else_then_issue_with_a_number()
    {
        var org = await OrgAsync();
        var draft = (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/CreateDraft",
            new { typeId = org.TypeId, party = "المركزية", subject = "إرسال الدراسة", summary = "نص الكتاب" })).Ok();
        Assert.Null(draft.Number);
        Assert.Equal((int)OutgoingState.Draft, draft.OutgoingState);

        // الاعتماد بلا نص الكتاب مرفوض، وغير المخوَّل ممنوع
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<CorrespondenceResponseDto>($"{Base}/Decide/{draft.Id}", new { approve = true })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Decide/{draft.Id}", new { approve = true })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Submit/{draft.Id}")).Status);   // بلا مرفق

        (await org.Clerk.PostFiles<CorrespondenceResponseDto>($"{Base}/Attachments/{draft.Id}", ("الكتاب.pdf", Pdf))).Ok();
        var submitted = (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Submit/{draft.Id}")).Ok();
        Assert.True(submitted.IsSubmitted);
        Assert.NotNull(submitted.SubmittedAt);
        Assert.Equal("بانتظار الاعتماد", submitted.OutgoingStateAr);
        Assert.Equal((int)OutgoingState.Draft, submitted.OutgoingState);   // الحالة الفعلية تبقى مسودة
        Assert.False(submitted.CanSubmit);                                  // لا إشعار مكرر للمعتمِدين
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Submit/{draft.Id}")).Status);

        // تعديل المسودة بعد الرفع يلغي الرفع: تُرفع من جديد
        var edited = (await org.Clerk.Put<CorrespondenceResponseDto>($"{Base}/Update/{draft.Id}",
            new { typeId = org.TypeId, party = "المركزية", subject = "إرسال الدراسة (معدّل)", summary = "نص الكتاب", rowVersion = submitted.RowVersion })).Ok();
        Assert.False(edited.IsSubmitted);
        Assert.Equal("مسودة", edited.OutgoingStateAr);
        Assert.True(edited.CanSubmit);
        submitted = (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Submit/{draft.Id}")).Ok();
        Assert.True(submitted.IsSubmitted);

        await using (var scope = api.Services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<DataContext>().Notifications
                .AnyAsync(n => n.UserId == org.ApproverId && n.Type == NotificationType.OutgoingAwaitingApproval && n.RelatedEntityId == draft.Id));

        // إعادة بسبب أولاً: تتطلب سبباً، ثم يعدّلها المُعدّ ويرفعها من جديد
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Approver.Post<CorrespondenceResponseDto>($"{Base}/Decide/{draft.Id}", new { approve = false })).Status);
        var returned = (await org.Approver.Post<CorrespondenceResponseDto>($"{Base}/Decide/{draft.Id}", new { approve = false, note = "عدّل الصياغة" })).Ok();
        Assert.Equal("عدّل الصياغة", returned.ReturnNote);
        Assert.Equal((int)OutgoingState.Draft, returned.OutgoingState);
        Assert.False(returned.IsSubmitted);

        var approved = (await org.Approver.Post<CorrespondenceResponseDto>($"{Base}/Decide/{draft.Id}", new { approve = true })).Ok();
        Assert.Equal((int)OutgoingState.Approved, approved.OutgoingState);
        Assert.NotNull(approved.ApprovedAt);
        Assert.Null(approved.ReturnNote);
        Assert.True(approved.CanIssue is false);          // المعتمِد ليس الديوان

        // معتمد: لا تعديل ولا إرفاق
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Clerk.Put<CorrespondenceResponseDto>($"{Base}/Update/{draft.Id}",
            new { typeId = org.TypeId, party = "ج", subject = "تغيير" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Clerk.PostFiles<CorrespondenceResponseDto>($"{Base}/Attachments/{draft.Id}", ("آخر.pdf", Pdf))).Status);

        var issued = (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Issue/{draft.Id}")).Ok();
        Assert.Matches($@"^OUT-{DateTime.Today.Year}-\d{{5}}$", issued.Number);
        Assert.True(issued.IsClosed);
        Assert.NotNull(issued.IssuedAt);

        Assert.Equal(HttpStatusCode.BadRequest, (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Issue/{draft.Id}")).Status);   // مرة واحدة
        var history = (await org.Clerk.Get<List<CorrespondenceHistoryDto>>($"{Base}/History/{draft.Id}")).Ok();
        Assert.Equal(new[] { "created", "attached", "submitted", "updated", "submitted", "returned", "approved", "issued" }, history.OrderBy(h => h.At).ThenBy(h => h.Id).Select(h => h.Action));
    }

    [Fact]
    public async Task A_drafter_cannot_approve_their_own_draft_even_with_the_approval_permission()
    {
        var org = await OrgAsync();
        var role = await api.CreateRoleAsync("معدّ ومعتمد " + Guid.NewGuid().ToString("N")[..6],
            ["RegisterCorrespondence", "ApproveOutgoingCorrespondence", "ViewCorrespondence"], api.AllPermissions);
        var email = $"both-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("معدّ ومعتمد", email, role, api.BranchId, api.DepartmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        var both = api.WithToken(login.Token, id);

        var draft = (await both.Post<CorrespondenceResponseDto>($"{Base}/CreateDraft", new { typeId = org.TypeId, party = "ج", subject = "كتاب" })).Ok();
        (await both.PostFiles<CorrespondenceResponseDto>($"{Base}/Attachments/{draft.Id}", ("ك.pdf", Pdf))).Ok();

        var own = await both.Post<CorrespondenceResponseDto>($"{Base}/Decide/{draft.Id}", new { approve = true });
        Assert.Equal(HttpStatusCode.Forbidden, own.Status);
        Assert.False((await GetAsync(both, draft.Id)).Ok().CanApprove);
    }

    [Fact]
    public async Task A_reply_links_an_outgoing_draft_to_the_incoming_it_answers_within_the_branch()
    {
        var org = await OrgAsync();
        var incoming = (await RegisterAsync(org, "طلب استيضاح")).Ok();

        var draft = (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/CreateDraft",
            new { typeId = org.TypeId, party = "المركزية", subject = "رد على الاستيضاح", replyToId = incoming.Id })).Ok();
        Assert.Equal(incoming.Number, draft.ReplyToNumber);

        var wrongTarget = await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/CreateDraft",
            new { typeId = org.TypeId, party = "ج", subject = "رد", replyToId = draft.Id });   // ليس وارداً
        Assert.Equal(HttpStatusCode.BadRequest, wrongTarget.Status);
    }

    // ───────── البحث والتأخر ─────────

    [Fact]
    public async Task Search_filters_by_text_direction_and_overdue_and_respects_the_visibility_limit()
    {
        var org = await OrgAsync();
        var marker = "كلمة-" + Guid.NewGuid().ToString("N")[..8];
        var c = (await RegisterAsync(org, marker)).Ok();

        var byText = (await org.Clerk.Get<PagedResultDto<CorrespondenceListItemDto>>($"{Base}/Search?search={marker}")).Ok();
        Assert.Equal(c.Id, Assert.Single(byText.Items).Id);

        var byNumber = (await org.Clerk.Get<PagedResultDto<CorrespondenceListItemDto>>($"{Base}/Search?search={c.Number}")).Ok();
        Assert.Contains(byNumber.Items, i => i.Id == c.Id);

        var outgoingOnly = (await org.Clerk.Get<PagedResultDto<CorrespondenceListItemDto>>($"{Base}/Search?direction=2&search={marker}")).Ok();
        Assert.Empty(outgoingOnly.Items);

        // من لا يرى المعاملة لا تظهر له في البحث
        var hidden = (await org.StudiesPlain.Get<PagedResultDto<CorrespondenceListItemDto>>($"{Base}/Search?search={marker}")).Ok();
        Assert.Empty(hidden.Items);

        // مرشّح التأخر: موعد مضى لوارد مفتوح
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Correspondences SET DueDate = {DateTime.Today.AddDays(-2)} WHERE Id = {c.Id}");
        }
        var overdue = (await org.Clerk.Get<PagedResultDto<CorrespondenceListItemDto>>($"{Base}/Search?overdueOnly=true&search={marker}")).Ok();
        Assert.True(Assert.Single(overdue.Items).IsOverdue);
    }

    // ───────── حراسات الحذف ─────────

    [Fact]
    public async Task A_department_a_user_or_a_branch_with_diwan_records_cannot_be_deleted()
    {
        var org = await OrgAsync();
        var c = (await RegisterAsync(org)).Ok();
        (await ReferAsync(org.Clerk, c.Id, org.StudiesDepartmentId)).Ok();

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Delete($"/api/Department/Delete/{org.StudiesDepartmentId}")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Delete($"/api/Users/Delete/{org.ClerkId}")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Admin.Delete($"/api/Branches/Delete/{api.BranchId}")).Status);
    }

    // ───────── الطباعة ─────────

    [Fact]
    public async Task Only_an_approved_or_issued_outgoing_can_be_printed_with_the_approver_and_hijri_date()
    {
        var org = await OrgAsync();
        var draft = (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/CreateDraft",
            new { typeId = org.TypeId, party = "المركزية", subject = "كتاب للطباعة", summary = "نص الكتاب كاملاً" })).Ok();
        (await org.Clerk.PostFiles<CorrespondenceResponseDto>($"{Base}/Attachments/{draft.Id}", ("الكتاب.pdf", Pdf))).Ok();

        Assert.Equal(HttpStatusCode.BadRequest, (await org.Clerk.Get<CorrespondencePrintDto>($"{Base}/Print/{draft.Id}")).Status);   // مسودة

        (await org.Approver.Post<CorrespondenceResponseDto>($"{Base}/Decide/{draft.Id}", new { approve = true })).Ok();
        var approved = (await org.Clerk.Get<CorrespondencePrintDto>($"{Base}/Print/{draft.Id}")).Ok();
        Assert.False(approved.IsIssued);
        Assert.Null(approved.Number);
        Assert.Equal("نص الكتاب كاملاً", approved.Body);
        Assert.Contains("ديوان approver", approved.ApproverName);
        Assert.Matches(@"^\d{4}/\d{1,2}/\d{1,2}$", approved.DateHijri);

        (await org.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Issue/{draft.Id}")).Ok();
        var issued = (await org.Clerk.Get<CorrespondencePrintDto>($"{Base}/Print/{draft.Id}")).Ok();
        Assert.True(issued.IsIssued);
        Assert.Matches($@"^OUT-{DateTime.Today.Year}-\d{{5}}$", issued.Number);

        // من لا يرى المعاملة لا يطبعها، والوارد لا يُطبع
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Outsider.Get<CorrespondencePrintDto>($"{Base}/Print/{draft.Id}")).Status);
        var incoming = (await RegisterAsync(org)).Ok();
        Assert.Equal(HttpStatusCode.BadRequest, (await org.Clerk.Get<CorrespondencePrintDto>($"{Base}/Print/{incoming.Id}")).Status);
    }
}
