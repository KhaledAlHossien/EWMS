using Application.DTOs.Request;
using Application.DTOs.Response;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// مشروع جديد لكل اختبار (فلا يتأثر اختبار بآخر) مع مراحل البناء المعتادة: أمر المباشرة ← مسودة بثلاثة بنود وتبعيتين ← اعتماد.
/// البنود على التواريخ الحالية: «حفر» انتهى قبل 15 يوماً، «أساسات» انتهى قبل 3 أيام (مرشَّح للتأخر)، «هيكل» يمتد إلى بعد 40 يوماً.
/// </summary>
public sealed class ScheduleScenario
{
    private readonly ApiFixture _api;
    private static int _sequence;

    private ScheduleScenario(ApiFixture api) => _api = api;

    public int ProjectId { get; private set; }
    public int ScheduleId { get; private set; }
    public ScheduleTaskResponseDto Dig { get; private set; } = null!;
    public ScheduleTaskResponseDto Foundations { get; private set; } = null!;
    public ScheduleTaskResponseDto Structure { get; private set; } = null!;

    public static DateTime Day(int offset) => DateTime.Today.AddDays(offset);

    /// <summary>مشروع بمعدّ الجدول مشاركاً فيه (منشئه) والمعتمد يراه بصلاحية الفرع</summary>
    public static async Task<ScheduleScenario> NewProjectAsync(ApiFixture api)
    {
        var project = (await api.Preparer.Post<ProjectResponseDto>("/api/Projects/Create", new ProjectRequestDto
        {
            Name = $"مشروع اختبار {Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..40],
            Beneficiary = "جهة اختبار",
            DepartmentId = api.DepartmentId
        })).Ok();
        return new ScheduleScenario(api) { ProjectId = project.Id };
    }

    public static StartOrderRequestDto StartOrder(int? engineerUserId = null) => new()
    {
        OrderNumber = "م/1", OrderDate = Day(-40), StartDate = Day(-35), CompletionDays = 200, ExecutionDays = 180, SiteEngineerUserId = engineerUserId
    };

    public static ScheduleTaskRequestDto TaskSpec(string name, int startOffset, int endOffset, decimal weight, int? assignee = null) => new()
    {
        Name = name, PlannedStart = Day(startOffset), PlannedEnd = Day(endOffset), Weight = weight, AssigneeUserId = assignee
    };

    public async Task<ScheduleScenario> WithStartOrderAsync()
    {
        (await _api.Preparer.Put<StartOrderResponseDto>($"/api/ProjectSchedules/StartOrder/{ProjectId}", StartOrder(_api.Preparer.UserId))).Ok();
        return this;
    }

    /// <summary>مسودة بثلاثة بنود (أوزان 2 و5 و3) وتبعيتين: حفر ← أساسات ← هيكل</summary>
    public async Task<ScheduleScenario> WithDraftAsync()
    {
        var draft = (await _api.Preparer.Post<ScheduleDetailResponseDto>($"/api/ProjectSchedules/Draft/{ProjectId}", new ScheduleDraftRequestDto { Title = "الجدول الأول" })).Ok();
        ScheduleId = draft.Id;
        Dig = await AddTaskAsync(TaskSpec("حفر", -34, -15, 2));
        Foundations = await AddTaskAsync(TaskSpec("أساسات", -14, -3, 5));
        Structure = await AddTaskAsync(TaskSpec("هيكل", -2, 40, 3));
        (await _api.Preparer.Post<ScheduleDependencyResponseDto>($"/api/ProjectSchedules/Dependencies/{ScheduleId}", new ScheduleDependencyRequestDto { PredecessorId = Dig.Id, SuccessorId = Foundations.Id })).Ok();
        (await _api.Preparer.Post<ScheduleDependencyResponseDto>($"/api/ProjectSchedules/Dependencies/{ScheduleId}", new ScheduleDependencyRequestDto { PredecessorId = Foundations.Id, SuccessorId = Structure.Id })).Ok();
        return this;
    }

    public async Task<ScheduleTaskResponseDto> AddTaskAsync(ScheduleTaskRequestDto task) =>
        (await _api.Preparer.Post<ScheduleTaskResponseDto>($"/api/ProjectSchedules/Tasks/{ScheduleId}", task)).Ok();

    /// <summary>المعتمد (غير من أعدّ) يعتمد المسودة فتثبت التواريخ الأصلية</summary>
    public async Task<ScheduleScenario> ApprovedAsync()
    {
        (await _api.Approver.Post<ScheduleDetailResponseDto>($"/api/ProjectSchedules/Approve/{ScheduleId}")).Ok();
        return this;
    }

    public async Task<ScheduleDetailResponseDto> ScheduleAsync(int? id = null) =>
        (await _api.Preparer.Get<ScheduleDetailResponseDto>($"/api/ProjectSchedules/Get/{id ?? ScheduleId}")).Ok();

    /// <summary>يرفع ملفاً صغيراً بالرفع المجزّأ الحقيقي وينتظر الإنهاء في الخلفية، ويعيد معرّف إصدار الوثيقة (مستند موافقة)</summary>
    public async Task<int> UploadDocumentAsync()
    {
        var content = "كتاب موافقة — نص تجريبي"u8.ToArray();
        var started = (await _api.Preparer.Post<ProjectUploadResponseDto>("/api/ProjectUploads/Start", new StartProjectUploadRequestDto
        {
            ProjectId = ProjectId, Title = "كتاب موافقة", Kind = 10, FileName = "approval.txt", Size = content.Length
        })).Ok();
        (await _api.Preparer.PutBytes<ProjectUploadResponseDto>($"/api/ProjectUploads/{started.Id}/Chunk?offset=0", content)).Ok();
        (await _api.Preparer.Post<ProjectUploadResponseDto>($"/api/ProjectUploads/{started.Id}/Complete")).Ok();

        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(500);
            var state = (await _api.Preparer.Get<ProjectUploadResponseDto>($"/api/ProjectUploads/{started.Id}")).Ok();
            if (state.Status == 3 && state.VersionId is int version) return version;
            if (state.Status == 4) throw new Xunit.Sdk.XunitException($"فشل إنهاء الرفع: {state.Error}");
        }
        throw new Xunit.Sdk.XunitException("لم ينتهِ الرفع في المهلة");
    }
}
