using System.Net;
using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.AssignedTasks;
using Application.Features.Diwan;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests;

/// <summary>
/// الديوان (2.3) على القاعدة الحقيقية: تذكيرات الموعد مرة لكل موعد وتُعاد بعد الإحالة، التقريران بحدّ الرؤية، وربط المعاملة بالمهام.
/// </summary>
[Collection(ApiCollection.Name)]
public class DiwanRemindersAndReportsTests(ApiFixture api)
{
    private const string Base = "/api/Correspondence";

    private sealed record Setup(ApiClient Clerk, int ClerkId, ApiClient Head, int HeadId, ApiClient Reporter, int TypeId, int RegisteredStatusId, int InProgressStatusId,
        int AnsweredStatusId, int StudiesDepartmentId);

    private static readonly object Gate = new();
    private static Task<Setup>? _shared;

    private Task<Setup> SetupAsync() { lock (Gate) return _shared ??= CreateAsync(); }

    private async Task<(ApiClient Client, int Id)> UserAsync(string label, int roleId, int departmentId)
    {
        var email = $"{label}-{Guid.NewGuid():N}"[..22] + "@test.sy";
        var id = await api.CreateUserAsync("ديوان " + label, email, roleId, api.BranchId, departmentId);
        await using var scope = api.Services.CreateAsyncScope();
        var login = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequest { Email = email, Password = ApiFixture.Password })).Response!;
        return (api.WithToken(login.Token, id), id);
    }

    private async Task<Setup> CreateAsync()
    {
        var all = api.AllPermissions;
        var studies = (await api.Admin.PostForm<DepartmentResponseDto>("/api/Department/Create", [
            new("Name", "دراسات التذكير " + Guid.NewGuid().ToString("N")[..6]), new("Description", "اختبار"), new("BranchId", api.BranchId.ToString())])).Ok();
        // قسم مستقل للديوان كي لا يشاركه معالجون من اختبارات أخرى فتتداخل الإشعارات
        var diwan = (await api.Admin.PostForm<DepartmentResponseDto>("/api/Department/Create", [
            new("Name", "ديوان التذكير " + Guid.NewGuid().ToString("N")[..6]), new("Description", "اختبار"), new("BranchId", api.BranchId.ToString())])).Ok();

        var clerkRole = await api.CreateRoleAsync("كاتب تذكير " + Guid.NewGuid().ToString("N")[..6],
            ["RegisterCorrespondence", "ReferCorrespondence", "HandleCorrespondence", "ViewCorrespondence"], all);
        var headRole = await api.CreateRoleAsync("رئيس تذكير " + Guid.NewGuid().ToString("N")[..6], ["HandleCorrespondence", "ViewCorrespondence"], all);
        var reporterRole = await api.CreateRoleAsync("مقرِّر ديوان " + Guid.NewGuid().ToString("N")[..6],
            ["ExportCorrespondenceReport", "ExportOverdueCorrespondenceReport", "ViewBranchCorrespondence"], all);

        var clerk = await UserAsync("rclerk", clerkRole, diwan.Id);
        var head = await UserAsync("rhead", headRole, studies.Id);
        var reporter = await UserAsync("rrep", reporterRole, diwan.Id);

        async Task<int> Status(string name, int stage) => (await api.Admin.Post<CorrespondenceStatusResponseDto>("/api/CorrespondenceStatuses/Create",
            new { branchId = api.BranchId, name = name + Guid.NewGuid().ToString("N")[..4], stage, color = "#34C759", isActive = true, sortOrder = stage })).Ok().Id;
        var registered = await Status("مسجّلة", 1);
        var progress = await Status("قيد المعالجة", 2);
        var answered = await Status("تمّت الإجابة", 3);
        var type = (await api.Admin.Post<CorrespondenceTypeResponseDto>("/api/CorrespondenceTypes/Create",
            new { branchId = api.BranchId, name = "تذكير " + Guid.NewGuid().ToString("N")[..6], deadlineDays = 3, isActive = true })).Ok();

        return new Setup(clerk.Client, clerk.Id, head.Client, head.Id, reporter.Client, type.Id, registered, progress, answered, studies.Id);
    }

    private async Task<CorrespondenceResponseDto> RegisterAsync(Setup s, string subject)
    {
        await SetupAsync();
        return (await s.Clerk.Post<CorrespondenceResponseDto>($"{Base}/RegisterIncoming", new { typeId = s.TypeId, party = "المركزية", subject })).Ok();
    }

    private async Task SetDueAsync(int id, DateTime due)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DataContext>().Database
            .ExecuteSqlInterpolatedAsync($"UPDATE Correspondences SET DueDate = {due}, DueSoonNotifiedAt = NULL, OverdueNotifiedAt = NULL WHERE Id = {id}");
    }

    private async Task<int> RunRemindersAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        return await DiwanReminders.RunAsync(sp.GetRequiredService<IDiwanService>(), sp.GetRequiredService<IUserPermissionService>(),
            sp.GetRequiredService<INotificationService>(), DateTime.Now);
    }

    private async Task<List<Notification>> NotificationsAsync(int correspondenceId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DataContext>().Notifications.AsNoTracking()
            .Where(n => n.RelatedEntityType == "Correspondence" && n.RelatedEntityId == correspondenceId
                        && (n.Type == NotificationType.CorrespondenceDueSoon || n.Type == NotificationType.CorrespondenceOverdue)).ToListAsync();
    }

    // ───────── التذكيرات ─────────

    [Fact]
    public async Task A_due_soon_item_notifies_its_handlers_once_and_an_overdue_one_notifies_again_once()
    {
        var s = await SetupAsync();
        var c = await RegisterAsync(s, "موعد قريب");
        await SetDueAsync(c.Id, DateTime.Today.AddDays(1));

        await RunRemindersAsync();
        var first = await NotificationsAsync(c.Id);
        var soon = Assert.Single(first);
        Assert.Equal(NotificationType.CorrespondenceDueSoon, soon.Type);
        Assert.Equal(s.ClerkId, soon.UserId);                       // يعالجها في وحدتها الحالية

        await RunRemindersAsync();
        Assert.Single(await NotificationsAsync(c.Id));              // لا تكرار لنفس الموعد

        await SetDueAsync(c.Id, DateTime.Today.AddDays(-3));        // مضى الموعد
        await RunRemindersAsync();
        var all = await NotificationsAsync(c.Id);
        Assert.Contains(all, n => n.Type == NotificationType.CorrespondenceOverdue && n.Message.Contains("3 أيام"));
        await RunRemindersAsync();
        Assert.Equal(2, (await NotificationsAsync(c.Id)).Count);
    }

    [Fact]
    public async Task After_a_referral_the_new_unit_is_reminded_of_an_overdue_item()
    {
        var s = await SetupAsync();
        var c = await RegisterAsync(s, "تأخر ثم إحالة");
        await SetDueAsync(c.Id, DateTime.Today.AddDays(-1));
        await RunRemindersAsync();
        Assert.Single(await NotificationsAsync(c.Id), n => n.UserId == s.ClerkId);

        (await s.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Refer/{c.Id}", new { toDepartmentId = s.StudiesDepartmentId })).Ok();
        await RunRemindersAsync();

        var reminders = await NotificationsAsync(c.Id);
        Assert.Contains(reminders, n => n.UserId == s.HeadId && n.Type == NotificationType.CorrespondenceOverdue);
    }

    [Fact]
    public async Task Answered_and_closed_items_and_outgoing_ones_are_never_reminded()
    {
        var s = await SetupAsync();
        var answered = await RegisterAsync(s, "أُجيب عنها");
        await SetDueAsync(answered.Id, DateTime.Today.AddDays(-5));
        (await s.Clerk.Put<CorrespondenceResponseDto>($"{Base}/Status/{answered.Id}", new { statusId = s.AnsweredStatusId })).Ok();

        var draft = (await s.Clerk.Post<CorrespondenceResponseDto>($"{Base}/CreateDraft", new { typeId = s.TypeId, party = "ج", subject = "مسودة" })).Ok();

        await RunRemindersAsync();

        Assert.Empty(await NotificationsAsync(answered.Id));
        Assert.Empty(await NotificationsAsync(draft.Id));
    }

    [Fact]
    public async Task A_unit_with_no_handler_sends_the_reminder_to_the_registrar_so_it_is_not_lost()
    {
        var s = await SetupAsync();
        var emptyDept = (await api.Admin.PostForm<DepartmentResponseDto>("/api/Department/Create", [
            new("Name", "قسم بلا معالج " + Guid.NewGuid().ToString("N")[..6]), new("Description", "اختبار"), new("BranchId", api.BranchId.ToString())])).Ok();
        var c = await RegisterAsync(s, "إلى قسم بلا معالج");
        (await s.Clerk.Post<CorrespondenceResponseDto>($"{Base}/Refer/{c.Id}", new { toDepartmentId = emptyDept.Id })).Ok();
        await SetDueAsync(c.Id, DateTime.Today.AddDays(-1));

        await RunRemindersAsync();

        Assert.Single(await NotificationsAsync(c.Id), n => n.UserId == s.ClerkId);
    }

    // ───────── التقارير ─────────

    private static Task<ApiResult<ReportPreviewDto>> PreviewAsync(ApiClient client, string key, Dictionary<string, string?>? filters = null) =>
        client.Post<ReportPreviewDto>($"/api/Reports/Preview/{key}", new { filters = filters ?? [] });

    private static string Cell(ReportPreviewDto preview, int row, int column) => ((JsonElement)preview.Table.Rows[row][column]!).ToString();

    [Fact]
    public async Task The_register_report_lists_what_the_viewer_sees_and_marks_late_items()
    {
        var s = await SetupAsync();
        var marker = "تقرير-" + Guid.NewGuid().ToString("N")[..8];
        var late = await RegisterAsync(s, marker + " متأخرة");
        var fresh = await RegisterAsync(s, marker + " عادية");
        await SetDueAsync(late.Id, DateTime.Today.AddDays(-4));

        var preview = (await PreviewAsync(s.Reporter, "correspondence", new() { ["text"] = marker })).Ok();

        Assert.Equal(2, preview.Table.Total);
        var subjects = Enumerable.Range(0, preview.Table.Rows.Count).Select(i => Cell(preview, i, 2)).ToList();
        Assert.All(subjects, t => Assert.Contains(marker, t));
        var lateRow = Enumerable.Range(0, preview.Table.Rows.Count).Single(i => Cell(preview, i, 2).Contains("متأخرة"));
        Assert.Equal("نعم", Cell(preview, lateRow, 10));
        var freshRow = Enumerable.Range(0, preview.Table.Rows.Count).Single(i => Cell(preview, i, 2).Contains("عادية"));
        Assert.Equal("لا", Cell(preview, freshRow, 10));
        Assert.Equal(fresh.Number, Cell(preview, freshRow, 0));
    }

    [Fact]
    public async Task The_overdue_report_shows_only_open_late_incoming_items_oldest_first_with_days_late()
    {
        var s = await SetupAsync();
        var marker = "تجاوز-" + Guid.NewGuid().ToString("N")[..8];
        var older = await RegisterAsync(s, marker + " الأقدم");
        var newer = await RegisterAsync(s, marker + " الأحدث");
        var done = await RegisterAsync(s, marker + " مُجابة");
        var onTime = await RegisterAsync(s, marker + " في موعدها");
        await SetDueAsync(older.Id, DateTime.Today.AddDays(-9));
        await SetDueAsync(newer.Id, DateTime.Today.AddDays(-2));
        await SetDueAsync(done.Id, DateTime.Today.AddDays(-7));
        (await s.Clerk.Put<CorrespondenceResponseDto>($"{Base}/Status/{done.Id}", new { statusId = s.AnsweredStatusId })).Ok();

        var preview = (await PreviewAsync(s.Reporter, "correspondence-overdue", new() { ["text"] = marker })).Ok();

        Assert.Equal(2, preview.Table.Total);
        Assert.Contains("الأقدم", Cell(preview, 0, 1));
        Assert.Equal("9", Cell(preview, 0, 9));
        Assert.Contains("الأحدث", Cell(preview, 1, 1));
        Assert.Equal("2", Cell(preview, 1, 9));
    }

    [Fact]
    public async Task Reports_need_their_own_export_permission_and_respect_the_visibility_limit()
    {
        var s = await SetupAsync();
        await RegisterAsync(s, "حدود التقرير");

        // الكاتب يرى معاملاته لكنه لا يملك صلاحية سحب التقرير
        Assert.Equal(HttpStatusCode.Forbidden, (await PreviewAsync(s.Clerk, "correspondence")).Status);
        // مستخدم من فرع آخر بلا أي صلاحية ديوان
        Assert.Equal(HttpStatusCode.Forbidden, (await PreviewAsync(api.Outsider, "correspondence-overdue")).Status);
    }

    // ───────── ربط المعاملة بالمهام ─────────

    private async Task<Application.Common.Viewer> ViewerOfAsync(int userId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserService>().GetByIdAsync(userId) ?? throw new InvalidOperationException("مستخدم الاختبار غير موجود");
        return new Application.Common.Viewer(user, await scope.ServiceProvider.GetRequiredService<IUserPermissionService>().GetAsync(userId));
    }

    [Fact]
    public async Task A_task_link_to_a_correspondence_resolves_its_number_and_hides_it_from_those_who_cannot_see_it()
    {
        var s = await SetupAsync();
        var c = await RegisterAsync(s, "ربط بمهمة");
        var clerk = await ViewerOfAsync(s.ClerkId);
        var plain = await ViewerOfAsync(api.Outsider.UserId);

        await using var scope = api.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<TaskLinkResolver>();

        // بالرقم الداخلي (تسلسل فرع لا معرّف) وبالمعرّف
        Assert.Equal(c.Id, await resolver.ResolveCorrespondenceReferenceAsync(clerk, c.Number));
        Assert.Equal(c.Id, await resolver.ResolveCorrespondenceReferenceAsync(clerk, c.Number!.ToLowerInvariant()));
        Assert.Equal(c.Id, await resolver.ResolveCorrespondenceReferenceAsync(clerk, c.Id.ToString()));
        Assert.Null(await resolver.ResolveCorrespondenceReferenceAsync(clerk, "IN-2099-99999"));
        Assert.Null(await resolver.ResolveCorrespondenceReferenceAsync(plain, c.Number));   // لا يراها فلا يهتدي إليها

        var seen = await resolver.DescribeAsync(clerk, TaskLinkType.Correspondence, c.Id);
        Assert.True(seen.Available);
        Assert.Contains(c.Number!, seen.Label);
        var hidden = await resolver.DescribeAsync(plain, TaskLinkType.Correspondence, c.Id);
        Assert.True(hidden.Exists);
        Assert.False(hidden.Available);
        Assert.Equal(string.Empty, hidden.Label);
        Assert.Equal("معاملة", TaskLinkResolver.TypeAr(TaskLinkType.Correspondence));
    }
}
