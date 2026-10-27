using System.Net;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Integration.Tests.Infrastructure;

namespace Integration.Tests;

/// <summary>الجدول الزمني من الطلب HTTP إلى قاعدة البيانات: الحدود، فصل الإعداد عن الاعتماد، تثبيت الأصل، والإنجاز</summary>
[Collection(ApiCollection.Name)]
public class ScheduleTests(ApiFixture api)
{
    private const string Base = "/api/ProjectSchedules";

    [Fact]
    public async Task Anonymous_callers_are_refused_and_the_other_branch_cannot_see_the_project()
    {
        var scenario = await ScheduleScenario.NewProjectAsync(api);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().Get<ScheduleOverviewResponseDto>($"{Base}/Overview/{scenario.ProjectId}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Outsider.Get<ScheduleOverviewResponseDto>($"{Base}/Overview/{scenario.ProjectId}")).Status);
        // المعتمد يرى مشاريع فرعه بصلاحية الفرع
        (await api.Approver.Get<ScheduleOverviewResponseDto>($"{Base}/Overview/{scenario.ProjectId}")).Ok();
    }

    [Fact]
    public async Task The_start_order_needs_project_members_and_valid_dates_and_belongs_to_the_editor_permission()
    {
        var scenario = await ScheduleScenario.NewProjectAsync(api);
        var path = $"{Base}/StartOrder/{scenario.ProjectId}";

        // المعتمد ليس مشاركاً في المشروع فلا يُختار مهندس موقع
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<StartOrderResponseDto>(path, ScheduleScenario.StartOrder(api.Approver.UserId))).Status);
        var early = ScheduleScenario.StartOrder();
        early.StartDate = ScheduleScenario.Day(-50);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<StartOrderResponseDto>(path, early)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Approver.Put<StartOrderResponseDto>(path, ScheduleScenario.StartOrder())).Status);

        var saved = (await api.Preparer.Put<StartOrderResponseDto>(path, ScheduleScenario.StartOrder(api.Preparer.UserId))).Ok();
        Assert.Equal("م/1", saved.OrderNumber);
        Assert.Equal(api.Preparer.UserId, saved.SiteEngineerUserId);
    }

    [Fact]
    public async Task Tasks_are_validated_and_dependencies_refuse_cycles_and_repeats()
    {
        var scenario = await (await ScheduleScenario.NewProjectAsync(api)).WithStartOrderAsync();
        await scenario.WithDraftAsync();
        var tasksPath = $"{Base}/Tasks/{scenario.ScheduleId}";

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleTaskResponseDto>(tasksPath, ScheduleScenario.TaskSpec("x", 0, 5, 0))).Status);          // وزن صفر
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleTaskResponseDto>(tasksPath, ScheduleScenario.TaskSpec("x", 5, 0, 1))).Status);          // نهاية قبل البداية
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleTaskResponseDto>(tasksPath, ScheduleScenario.TaskSpec("x", 0, 5, 1, api.Approver.UserId))).Status); // مسؤول غير مشارك

        var depPath = $"{Base}/Dependencies/{scenario.ScheduleId}";
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleDependencyResponseDto>(depPath,
            new ScheduleDependencyRequestDto { PredecessorId = scenario.Structure.Id, SuccessorId = scenario.Dig.Id })).Status);        // حلقة غير مباشرة
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleDependencyResponseDto>(depPath,
            new ScheduleDependencyRequestDto { PredecessorId = scenario.Dig.Id, SuccessorId = scenario.Foundations.Id })).Status);      // مكررة

        var ids = new[] { scenario.Foundations.Id, scenario.Dig.Id, scenario.Structure.Id };
        Assert.Equal(HttpStatusCode.NoContent, (await api.Preparer.Put<System.Text.Json.JsonElement>($"{Base}/Reorder/{scenario.ScheduleId}", new ReorderScheduleTasksRequestDto { TaskIds = [.. ids] })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<System.Text.Json.JsonElement>($"{Base}/Reorder/{scenario.ScheduleId}", new ReorderScheduleTasksRequestDto { TaskIds = [ids[0]] })).Status);
        var order = (await scenario.ScheduleAsync()).Tasks.Select(t => t.Id).ToArray();
        Assert.Equal(ids, order);
    }

    [Fact]
    public async Task The_author_cannot_approve_and_approval_fixes_the_original_dates_for_good()
    {
        var scenario = await (await ScheduleScenario.NewProjectAsync(api)).WithStartOrderAsync();
        await scenario.WithDraftAsync();

        // من أعدّ الجدول لا يملك صلاحية الاعتماد أصلاً، وحتى لو ملكها فالقاعدة تمنعه (مغطّاة في اختبارات الوحدة)
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Preparer.Post<ScheduleDetailResponseDto>($"{Base}/Approve/{scenario.ScheduleId}")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Post<ScheduleDetailResponseDto>($"{Base}/Draft/{scenario.ProjectId}", new ScheduleDraftRequestDto())).Status);   // مسودة ثانية

        await scenario.ApprovedAsync();
        var approved = await scenario.ScheduleAsync();
        var foundations = approved.Tasks.Single(t => t.Id == scenario.Foundations.Id);

        Assert.Equal(ScheduleScenario.Day(-14), foundations.OriginalStart);
        Assert.Equal(ScheduleScenario.Day(-3), foundations.OriginalEnd);
        Assert.True(foundations.DatesLocked);
        Assert.Equal(2, approved.OverdueCandidates);                           // «حفر» و«أساسات» تجاوزا نهايتهما ولم يكتملا

        // الجدول المعتمد لا يُعدَّل كالمسودة ولا يُحذف
        var edit = ScheduleScenario.TaskSpec("أساسات", -14, -3, 5, api.Preparer.UserId);
        edit.RowVersion = foundations.RowVersion;
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Task/{foundations.Id}", edit)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Delete($"{Base}/Draft/{scenario.ScheduleId}")).Status);
    }

    [Fact]
    public async Task Progress_is_validated_weighted_recorded_and_classification_is_audited()
    {
        var scenario = await (await ScheduleScenario.NewProjectAsync(api)).WithStartOrderAsync();
        await (await scenario.WithDraftAsync()).ApprovedAsync();

        // على المسودة لا إنجاز: مسودة أخرى لا تُحدَّث، وقيم متناقضة مرفوضة
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Progress/{scenario.Dig.Id}",
            new UpdateScheduleProgressRequestDto { Status = 2, Progress = 100 })).Status);                      // 100% بحالة «قيد التنفيذ»

        var done = (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Progress/{scenario.Dig.Id}",
            new UpdateScheduleProgressRequestDto { Status = 3, Progress = 100, ActualStart = ScheduleScenario.Day(-34), ActualEnd = ScheduleScenario.Day(-16), Note = "تم" })).Ok();
        Assert.Equal(ScheduleScenario.Day(-16), done.ActualEnd);

        var half = (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Progress/{scenario.Foundations.Id}",
            new UpdateScheduleProgressRequestDto { Status = 2, Progress = 60 })).Ok();
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Progress/{scenario.Foundations.Id}",
            new UpdateScheduleProgressRequestDto { Status = 2, Progress = 30 })).Status);                       // خفض بلا ملاحظة
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Progress/{scenario.Foundations.Id}",
            new UpdateScheduleProgressRequestDto { Status = 2, Progress = 70, ActualStart = ScheduleScenario.Day(3) })).Status);   // تاريخ فعلي مستقبلي

        var history = (await api.Preparer.Get<List<ScheduleProgressEntryResponseDto>>($"{Base}/Progress/{scenario.Foundations.Id}")).Ok();
        Assert.Single(history);
        Assert.Equal(60, history[0].Progress);

        var critical = (await api.Preparer.Put<ScheduleTaskResponseDto>($"{Base}/Classify/{scenario.Foundations.Id}",
            new ClassifyScheduleTaskRequestDto { IsCritical = true, RowVersion = half.RowVersion })).Ok();
        Assert.True(critical.IsCritical);

        // (100×2 + 60×5 + 0×3) / 10 = 50
        var overview = (await api.Preparer.Get<ScheduleOverviewResponseDto>($"{Base}/Overview/{scenario.ProjectId}")).Ok();
        Assert.Equal(50m, overview.ActualProgress);
    }
}
