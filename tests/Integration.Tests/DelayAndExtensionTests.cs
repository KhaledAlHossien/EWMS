using System.Net;
using System.Text.Json;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Integration.Tests.Infrastructure;

namespace Integration.Tests;

/// <summary>
/// التأخير والتمديد وإعادة الإصدار على قاعدة حقيقية. أحدها «حارس الانحدار» لخطأ وجدناه في الاختبار الحيّ:
/// رفع التبرير بمستند مقروء بلا تتبّع أدخل نسخة ثانية من المشروع في تتبّع EF فرُفض الحفظ، ولم تلتقطه اختبارات الوحدة بخدمات وهمية.
/// </summary>
[Collection(ApiCollection.Name)]
public class DelayAndExtensionTests(ApiFixture api)
{
    private const string Base = "/api/ProjectSchedules";

    private static JustifyScheduleDelayRequestDto Justification(int documentVersionId, params int[] affected) => new()
    {
        Reason = "إغلاق الطريق", ApprovalAuthority = "إدارة المشروع", ApprovalDate = ScheduleScenario.Day(-1),
        ApprovalDocumentVersionId = documentVersionId, AffectsCriticalWork = true, AffectedTaskIds = [.. affected]
    };

    private async Task<ScheduleScenario> ApprovedScenarioAsync()
    {
        var scenario = await ScheduleScenario.NewProjectAsync(api);
        await scenario.WithStartOrderAsync();
        await scenario.WithDraftAsync();
        return await scenario.ApprovedAsync();
    }

    [Fact]
    public async Task A_delay_is_recorded_deliberately_justified_by_its_recorder_and_decided_by_someone_else()
    {
        var scenario = await ApprovedScenarioAsync();
        var delayPath = $"{Base}/RecordDelay/{scenario.Foundations.Id}";

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleDelayResponseDto>(delayPath, new ScheduleDelayRequestDto { Kind = 1, DiscoveredAt = ScheduleScenario.Day(0) })).Status);    // بلا ملاحظة
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleDelayResponseDto>(delayPath, new ScheduleDelayRequestDto { Kind = 1, DiscoveredAt = ScheduleScenario.Day(2), Reason = "x" })).Status); // مستقبلي
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Approver.Post<ScheduleDelayResponseDto>(delayPath, new ScheduleDelayRequestDto { Kind = 2, DiscoveredAt = ScheduleScenario.Day(0), Reason = "x" })).Status);

        var delay = (await api.Preparer.Post<ScheduleDelayResponseDto>(delayPath, new ScheduleDelayRequestDto
        {
            Kind = 2, DiscoveredAt = ScheduleScenario.Day(0), Reason = "تأخر وصول الحديد", Actions = "مراسلة المورد", AffectedTaskIds = [scenario.Structure.Id]
        })).Ok();
        Assert.Equal(3, delay.DaysLate);
        Assert.Single(delay.AffectedTasks);

        // تأخير ثانٍ حيّ على البند نفسه مرفوض
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleDelayResponseDto>(delayPath, new ScheduleDelayRequestDto { Kind = 1, DiscoveredAt = ScheduleScenario.Day(0), Reason = "x" })).Status);

        var version = await scenario.UploadDocumentAsync();
        var justifyPath = $"{Base}/Delay/{delay.Id}/Justify";
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<ScheduleDelayResponseDto>(justifyPath, new JustifyScheduleDelayRequestDto
            { Reason = "x", ApprovalAuthority = "y", ApprovalDate = ScheduleScenario.Day(-1) })).Status);       // بلا مستند

        // حارس الانحدار: كان هنا يفشل بخطأ تتبّع EF
        var justified = (await api.Preparer.Put<ScheduleDelayResponseDto>(justifyPath, Justification(version, scenario.Structure.Id))).Ok();
        Assert.Equal(2, justified.Status);
        Assert.Equal("كتاب موافقة", justified.ApprovalDocumentTitle);

        var decidePath = $"{Base}/Delay/{delay.Id}/Decide";
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Preparer.Put<ScheduleDelayResponseDto>(decidePath, new ScheduleDecisionRequestDto { Approve = true })).Status);        // لا صلاحية له
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Approver.Put<ScheduleDelayResponseDto>(decidePath, new ScheduleDecisionRequestDto { Approve = false })).Status);      // رفض بلا سبب
        var decided = (await api.Approver.Put<ScheduleDelayResponseDto>(decidePath, new ScheduleDecisionRequestDto { Approve = true, Note = "موافق" })).Ok();
        Assert.Equal(3, decided.Status);
        Assert.NotNull(decided.DecidedByName);                                                                  // القاضي يظهر بعد إعادة القراءة
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Approver.Put<ScheduleDelayResponseDto>(decidePath, new ScheduleDecisionRequestDto { Approve = true })).Status);   // لا قرار ثانٍ
    }

    [Fact]
    public async Task An_extension_moves_the_current_dates_keeps_the_originals_and_ends_the_delay()
    {
        var scenario = await ApprovedScenarioAsync();
        var delay = (await api.Preparer.Post<ScheduleDelayResponseDto>($"{Base}/RecordDelay/{scenario.Foundations.Id}",
            new ScheduleDelayRequestDto { Kind = 2, DiscoveredAt = ScheduleScenario.Day(0), Reason = "ظروف" })).Ok();
        var version = await scenario.UploadDocumentAsync();
        (await api.Preparer.Put<ScheduleDelayResponseDto>($"{Base}/Delay/{delay.Id}/Justify", Justification(version))).Ok();
        (await api.Approver.Put<ScheduleDelayResponseDto>($"{Base}/Delay/{delay.Id}/Decide", new ScheduleDecisionRequestDto { Approve = true })).Ok();

        ExtensionLineRequestDto Line(int id, bool moves = false) => new() { TaskId = id, Days = 5, MovesStart = moves };
        ScheduleExtensionRequestDto Request(params ExtensionLineRequestDto[] lines) => new()
        {
            DelayId = delay.Id, Mode = 1, DayCount = 1, Reason = "تأخر التوريد", Lines = [.. lines], DocumentVersionIds = [version]
        };

        // المعاينة لا تغيّر شيئاً وتُظهر التعارض واللاحق المقترح (لا يُختار تلقائياً)
        var preview = (await api.Preparer.Post<ScheduleExtensionPreviewResponseDto>($"{Base}/Extensions/{scenario.ProjectId}/Preview", Request(Line(scenario.Foundations.Id)))).Ok();
        Assert.Equal(ScheduleScenario.Day(2), preview.Items.Single().NewEnd);
        Assert.Single(preview.Conflicts);
        Assert.Contains(preview.SuggestedTasks, s => s.TaskId == scenario.Structure.Id);
        Assert.Equal(ScheduleScenario.Day(-3), (await scenario.ScheduleAsync()).Tasks.Single(t => t.Id == scenario.Foundations.Id).PlannedEnd);

        // أيام خارج الحد، وتمديد جماعي بأيام مختلفة
        var previewPath = $"{Base}/Extensions/{scenario.ProjectId}/Preview";
        var zeroDays = Request(Line(scenario.Foundations.Id));
        zeroDays.Lines[0].Days = 0;
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleExtensionPreviewResponseDto>(previewPath, zeroDays)).Status);
        var mixed = Request(Line(scenario.Foundations.Id), Line(scenario.Structure.Id));
        mixed.Lines[1].Days = 7;
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleExtensionPreviewResponseDto>(previewPath, mixed)).Status);

        var draft = (await api.Preparer.Post<ScheduleExtensionResponseDto>($"{Base}/Extensions/{scenario.ProjectId}", Request(Line(scenario.Foundations.Id), Line(scenario.Structure.Id, moves: true)))).Ok();
        Assert.Equal(1, draft.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleExtensionResponseDto>($"{Base}/Extensions/{scenario.ProjectId}", Request(Line(scenario.Foundations.Id)))).Status);       // طلب ثانٍ غير مبتوت فيه
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleDetailResponseDto>($"{Base}/Draft/{scenario.ProjectId}", new ScheduleDraftRequestDto())).Status);                  // لا إصدار جديد الآن
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Approver.Put<ScheduleExtensionResponseDto>($"{Base}/Extension/{draft.Id}", Request(Line(scenario.Foundations.Id)))).Status);

        var submitted = (await api.Preparer.Post<ScheduleExtensionResponseDto>($"{Base}/Extension/{draft.Id}/Submit")).Ok();
        Assert.Equal(2, submitted.Status);
        var decidePath = $"{Base}/Extension/{draft.Id}/Decide";
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Preparer.Put<ScheduleExtensionResponseDto>(decidePath, new ScheduleDecisionRequestDto { Approve = true })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Approver.Put<ScheduleExtensionResponseDto>(decidePath, new ScheduleDecisionRequestDto { Approve = false })).Status);

        var approved = (await api.Approver.Put<ScheduleExtensionResponseDto>(decidePath, new ScheduleDecisionRequestDto { Approve = true, Note = "اعتماد" })).Ok();
        Assert.Equal(3, approved.Status);

        var after = await scenario.ScheduleAsync();
        var foundations = after.Tasks.Single(t => t.Id == scenario.Foundations.Id);
        var structure = after.Tasks.Single(t => t.Id == scenario.Structure.Id);
        Assert.Equal(ScheduleScenario.Day(2), foundations.PlannedEnd);                  // الحالي تحرّك
        Assert.Equal(ScheduleScenario.Day(-3), foundations.OriginalEnd);                // الأصلي ثابت
        Assert.Equal(5, foundations.ExtraDays);
        Assert.Equal(ScheduleScenario.Day(3), structure.PlannedStart);                  // الإزاحة نقلت البداية والنهاية معاً
        Assert.Equal(ScheduleScenario.Day(45), structure.PlannedEnd);
        Assert.Equal(ScheduleScenario.Day(-2), structure.OriginalStart);
        Assert.Equal(5, (await api.Preparer.Get<ScheduleDelayResponseDto>($"{Base}/Delay/{delay.Id}")).Ok().Status);      // انتهى أثر التأخير
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Approver.Put<ScheduleExtensionResponseDto>(decidePath, new ScheduleDecisionRequestDto { Approve = true })).Status);
    }

    [Fact]
    public async Task A_revision_copies_the_tasks_with_their_baseline_and_supersedes_the_approved_version()
    {
        var scenario = await ApprovedScenarioAsync();

        var revision = (await api.Preparer.Post<ScheduleDetailResponseDto>($"{Base}/Draft/{scenario.ProjectId}", new ScheduleDraftRequestDto { Title = "مراجعة" })).Ok();
        Assert.Equal(3, revision.Tasks.Count);
        Assert.All(revision.Tasks, t => Assert.NotNull(t.OriginalStart));
        Assert.Equal(2, revision.Dependencies.Count);

        var foundations = revision.Tasks.Single(t => t.Name == "أساسات");
        var moveDates = ScheduleScenario.TaskSpec("أساسات", -14, 10, 5, api.Preparer.UserId);
        moveDates.RowVersion = foundations.RowVersion;
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Task/{foundations.Id}", moveDates)).Status);     // التواريخ مقفلة

        var reweight = ScheduleScenario.TaskSpec("أساسات", -14, -3, 6, api.Preparer.UserId);
        reweight.RowVersion = foundations.RowVersion;
        Assert.Equal(6m, (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Task/{foundations.Id}", reweight)).Ok().Weight);

        (await api.Approver.Post<ScheduleDetailResponseDto>($"{Base}/Approve/{revision.Id}")).Ok();

        var overview = (await api.Preparer.Get<ScheduleOverviewResponseDto>($"{Base}/Overview/{scenario.ProjectId}")).Ok();
        Assert.Single(overview.Versions, v => v.Status == 2);                                 // معتمد واحد فقط
        Assert.Equal(3, overview.Versions.Single(v => v.Id == scenario.ScheduleId).Status);   // والسابق «سابق»
        var carried = (await scenario.ScheduleAsync(revision.Id)).Tasks.Single(t => t.Name == "أساسات");
        Assert.Equal(ScheduleScenario.Day(-3), carried.OriginalEnd);                          // خط الأساس لم يضع
    }

    [Fact]
    public async Task Every_write_leaves_one_audit_entry_and_the_decision_notices_reach_the_right_people()
    {
        var scenario = await ApprovedScenarioAsync();
        var delay = (await api.Preparer.Post<ScheduleDelayResponseDto>($"{Base}/RecordDelay/{scenario.Foundations.Id}",
            new ScheduleDelayRequestDto { Kind = 2, DiscoveredAt = ScheduleScenario.Day(0), Reason = "ظروف" })).Ok();
        var version = await scenario.UploadDocumentAsync();
        (await api.Preparer.Put<ScheduleDelayResponseDto>($"{Base}/Delay/{delay.Id}/Justify", Justification(version))).Ok();
        (await api.Approver.Put<ScheduleDelayResponseDto>($"{Base}/Delay/{delay.Id}/Decide", new ScheduleDecisionRequestDto { Approve = true })).Ok();

        var audit = (await api.Admin.Get<PagedResultDto<AuditLogResponseDto>>($"/api/Projects/Audit/{scenario.ProjectId}?page=1&pageSize=100")).Ok();
        var kinds = audit.Items.Select(a => $"{a.EntityType}/{a.Action}").ToList();
        foreach (var expected in new[]
        {
            "Project/created", "ProjectStartOrder/created", "ProjectSchedule/created", "ProjectSchedule/approved", "ScheduleTask/created",
            "ScheduleDependency/created", "ProjectDocumentVersion/uploaded", "ScheduleDelay/created", "ScheduleDelay/justified", "ScheduleDelay/approved"
        })
            Assert.Contains(expected, kinds);
        Assert.Equal(3, kinds.Count(k => k == "ScheduleTask/created"));

        var approverNotices = (await api.Approver.Get<List<NotificationResponseDto>>("/api/Notifications/My")).Ok().Select(n => n.Type).ToList();
        var preparerNotices = (await api.Preparer.Get<List<NotificationResponseDto>>("/api/Notifications/My")).Ok().Select(n => n.Type).ToList();
        Assert.Contains("ScheduleJustificationSubmitted", approverNotices);
        Assert.Contains("ScheduleJustificationDecided", preparerNotices);
        Assert.DoesNotContain("ScheduleJustificationDecided", approverNotices);
    }
}
