using Application.Common;
using Application.Features.Schedules;
using Domain.Entities;
using static Application.Features.Schedules.ScheduleRules;

namespace Projects.Tests;

/// <summary>قواعد الجدول الزمني والتأخير والتمديد الصرفة: التواريخ، الأوزان اليدوية، التبعية، التمديد ومعاينته، والتبرير</summary>
public class ScheduleRulesTests
{
    private static readonly IReadOnlySet<DateTime> NoHolidays = new HashSet<DateTime>();
    private static DateTime D(int month, int day) => new(2026, month, day);   // 2026-10-01 خميس، 2026-10-02 جمعة

    private static ScheduleTask Task(int id, string name, DateTime start, DateTime end, decimal weight = 1, int progress = 0,
        ScheduleTaskStatus status = ScheduleTaskStatus.NotStarted) => new()
    {
        Id = id, Name = name, PlannedStart = start, PlannedEnd = end, Weight = weight, Progress = progress, Status = status
    };

    private static Dictionary<int, ScheduleTask> ById(params ScheduleTask[] tasks) => tasks.ToDictionary(t => t.Id);

    private static Viewer ViewerOf(int id) => new(
        new User { Id = id, FullName = "u", Email = "u@x.sy", PasswordHash = "x", RoleId = 1, Role = new Role { Id = 1, Name = "Emp" } },
        new HashSet<string>());

    // ───────── التواريخ ─────────

    [Fact]
    public void Calendar_days_include_both_ends()
    {
        Assert.Equal(1, CalendarDays(D(10, 5), D(10, 5)));
        Assert.Equal(10, CalendarDays(D(10, 1), D(10, 10)));
    }

    [Fact]
    public void Calendar_extension_adds_plain_days()
    {
        Assert.Equal(D(10, 11), AddDays(D(10, 1), 10, ExtensionDayCount.Calendar, NoHolidays));
        Assert.Equal(D(10, 1), AddDays(D(10, 1), 0, ExtensionDayCount.Calendar, NoHolidays));
    }

    [Fact]
    public void Working_day_extension_skips_fridays_and_holidays()
    {
        // الخميس 1/10: يوم عمل واحد بعده = السبت 3/10 (الجمعة 2/10 لا تُحسب)
        Assert.Equal(D(10, 3), AddDays(D(10, 1), 1, ExtensionDayCount.WorkingDays, NoHolidays));
        // خمسة أيام عمل من الخميس: السبت والأحد والإثنين والثلاثاء والأربعاء = الأربعاء 7/10
        Assert.Equal(D(10, 7), AddDays(D(10, 1), 5, ExtensionDayCount.WorkingDays, NoHolidays));
        // عطلة رسمية يوم الأحد 4/10 تُحذف من العدّ: السبت 3، ثم الإثنين 5
        Assert.Equal(D(10, 5), AddDays(D(10, 1), 2, ExtensionDayCount.WorkingDays, new HashSet<DateTime> { D(10, 4) }));
    }

    [Fact]
    public void Negative_extension_days_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AddDays(D(10, 1), -1, ExtensionDayCount.Calendar, NoHolidays));
    }

    // ───────── البنود والأوزان ─────────

    [Theory]
    [InlineData("بند", 1, 5, 1, true)]
    [InlineData("", 1, 5, 1, false)]
    [InlineData("بند", 5, 1, 1, false)]      // نهاية قبل البداية
    [InlineData("بند", 1, 5, 0, false)]      // وزن صفر
    [InlineData("بند", 1, 5, -2, false)]
    [InlineData("بند", 1, 1, 2.5, true)]     // يوم واحد، وزن عشري
    public void Task_data_is_validated(string name, int startDay, int endDay, double weight, bool valid) =>
        Assert.Equal(valid, ValidateTask(name, D(10, startDay), D(10, endDay), (decimal)weight) == null);

    [Fact]
    public void Task_name_and_weight_have_upper_limits()
    {
        Assert.NotNull(ValidateTask(new string('x', 301), D(10, 1), D(10, 2), 1));
        Assert.NotNull(ValidateTask("بند", D(10, 1), D(10, 2), MaxWeight + 1));
    }

    [Fact]
    public void Project_progress_is_the_weighted_average_of_manual_weights()
    {
        var tasks = new[]
        {
            Task(1, "أ", D(10, 1), D(10, 10), weight: 3, progress: 100, status: ScheduleTaskStatus.Completed),
            Task(2, "ب", D(10, 11), D(10, 20), weight: 1, progress: 0)
        };
        Assert.Equal(75m, ActualProgress(tasks));                 // (100×3 + 0×1) / 4
        Assert.Equal(0m, ActualProgress([]));
    }

    [Fact]
    public void A_heavier_weight_moves_the_project_percentage_more()
    {
        ScheduleTask Pair(decimal wA) => Task(1, "أ", D(10, 1), D(10, 5), wA, 100, ScheduleTaskStatus.Completed);
        var other = Task(2, "ب", D(10, 6), D(10, 10), 1, 0);
        Assert.True(ActualProgress([Pair(5), other]) > ActualProgress([Pair(1), other]));
    }

    [Fact]
    public void Planned_progress_follows_the_dates_with_the_same_weights()
    {
        var tasks = new[]
        {
            Task(1, "أ", D(10, 1), D(10, 10), weight: 1),     // 10 أيام
            Task(2, "ب", D(10, 11), D(10, 20), weight: 1)
        };
        Assert.Equal(0m, PlannedProgress(tasks, D(9, 30)));    // قبل البداية
        Assert.Equal(25m, PlannedProgress(tasks, D(10, 5)));   // نصف الأول (5/10) ÷ 2
        Assert.Equal(50m, PlannedProgress(tasks, D(10, 10)));  // الأول كله
        Assert.Equal(100m, PlannedProgress(tasks, D(10, 25)));
    }

    [Fact]
    public void Planned_progress_can_be_measured_against_the_original_baseline()
    {
        var t = Task(1, "أ", D(10, 11), D(10, 20));          // مُدِّد: نهايته الحالية 20 والأصلية 10
        t.OriginalStart = D(10, 1);
        t.OriginalEnd = D(10, 10);
        Assert.Equal(100m, PlannedProgress([t], D(10, 10), useOriginal: true));
        Assert.Equal(0m, PlannedProgress([t], D(10, 10), useOriginal: false));
    }

    // ───────── التقدم والحالة ─────────

    [Theory]
    [InlineData(ScheduleTaskStatus.Completed, 100, true)]
    [InlineData(ScheduleTaskStatus.Completed, 90, false)]
    [InlineData(ScheduleTaskStatus.InProgress, 100, false)]   // 100% يعني مكتمل
    [InlineData(ScheduleTaskStatus.InProgress, 40, true)]
    [InlineData(ScheduleTaskStatus.NotStarted, 10, false)]
    [InlineData(ScheduleTaskStatus.NotStarted, 0, true)]
    [InlineData(ScheduleTaskStatus.Suspended, 30, true)]
    [InlineData(ScheduleTaskStatus.InProgress, 101, false)]
    [InlineData(ScheduleTaskStatus.InProgress, -1, false)]
    public void Progress_and_status_must_agree(ScheduleTaskStatus status, int progress, bool valid) =>
        Assert.Equal(valid, ValidateProgress(status, progress, null, null, D(10, 20)) == null);

    [Fact]
    public void Actual_dates_cannot_be_in_the_future_or_reversed()
    {
        var today = D(10, 20);
        Assert.NotNull(ValidateProgress(ScheduleTaskStatus.InProgress, 10, D(10, 21), null, today));
        Assert.NotNull(ValidateProgress(ScheduleTaskStatus.Completed, 100, D(10, 15), D(10, 10), today));
        Assert.Null(ValidateProgress(ScheduleTaskStatus.Completed, 100, D(10, 10), D(10, 15), today));
    }

    [Fact]
    public void Actual_dates_are_completed_from_the_status_without_erasing_recorded_ones()
    {
        var today = D(10, 20);
        Assert.Equal((today, (DateTime?)null), CompleteActualDates(ScheduleTaskStatus.InProgress, null, null, today));
        Assert.Equal((D(10, 5), (DateTime?)null), CompleteActualDates(ScheduleTaskStatus.InProgress, D(10, 5), null, today));
        Assert.Equal((D(10, 5), (DateTime?)today), CompleteActualDates(ScheduleTaskStatus.Completed, D(10, 5), null, today));
        Assert.Equal((today, (DateTime?)today), CompleteActualDates(ScheduleTaskStatus.Completed, null, null, today));
        Assert.Equal(((DateTime?)null, (DateTime?)null), CompleteActualDates(ScheduleTaskStatus.NotStarted, D(10, 5), D(10, 9), today));
    }

    // ───────── مرشَّح التأخر ─────────

    [Theory]
    [InlineData(ScheduleTaskStatus.NotStarted, true)]
    [InlineData(ScheduleTaskStatus.InProgress, true)]
    [InlineData(ScheduleTaskStatus.Completed, false)]
    [InlineData(ScheduleTaskStatus.Suspended, false)]
    public void Only_unfinished_active_tasks_past_their_end_are_candidates(ScheduleTaskStatus status, bool expected) =>
        Assert.Equal(expected, IsOverdueCandidate(Task(1, "أ", D(10, 1), D(10, 10), status: status), today: D(10, 11)));

    [Fact]
    public void A_task_is_not_a_candidate_on_its_last_day_or_before()
    {
        var t = Task(1, "أ", D(10, 1), D(10, 10));
        Assert.False(IsOverdueCandidate(t, D(10, 10)));
        Assert.False(IsOverdueCandidate(t, D(10, 5)));
        Assert.True(IsOverdueCandidate(t, D(10, 11)));
    }

    [Fact]
    public void Extra_days_are_the_gap_between_current_and_original_end()
    {
        var t = Task(1, "أ", D(10, 1), D(10, 15));
        Assert.Equal(0, ExtraDays(t));                 // لم يُعتمد بعد: لا أصل
        t.OriginalEnd = D(10, 10);
        Assert.Equal(5, ExtraDays(t));
        t.PlannedEnd = D(10, 8);
        Assert.Equal(0, ExtraDays(t));
    }

    // ───────── التبعية ─────────

    [Fact]
    public void Dependency_cycles_are_detected_directly_and_through_a_chain()
    {
        var edges = new[] { new Edge(1, 2), new Edge(2, 3) };
        Assert.True(WouldCreateCycle(edges, 3, 1));     // 1→2→3 ثم 3→1
        Assert.True(WouldCreateCycle(edges, 2, 1));
        Assert.True(WouldCreateCycle(edges, 4, 4));     // على نفسه
        Assert.False(WouldCreateCycle(edges, 1, 3));    // اختصار لا حلقة
        Assert.False(WouldCreateCycle(edges, 3, 4));
    }

    [Fact]
    public void Successors_are_transitive_and_exclude_the_start()
    {
        var edges = new[] { new Edge(1, 2), new Edge(2, 3), new Edge(1, 4), new Edge(5, 6) };
        Assert.Equal(new HashSet<int> { 2, 3, 4 }, ScheduleRules.TransitiveSuccessors(edges, [1]));
        Assert.Equal(new HashSet<int> { 3 }, ScheduleRules.TransitiveSuccessors(edges, [2]));
        Assert.Empty(ScheduleRules.TransitiveSuccessors(edges, [6]));
    }

    [Fact]
    public void A_successor_starting_before_its_predecessor_ends_is_a_conflict()
    {
        var tasks = ById(Task(1, "أ", D(10, 1), D(10, 10)), Task(2, "ب", D(10, 10), D(10, 20)), Task(3, "ج", D(10, 11), D(10, 30)));
        var conflicts = DependencyConflicts(tasks, [new Edge(1, 2), new Edge(1, 3)]);
        Assert.Single(conflicts);
        Assert.Equal(new Edge(1, 2), conflicts[0]);   // ب تبدأ في يوم نهاية أ نفسه
    }

    // ───────── اعتماد الإصدار ─────────

    [Fact]
    public void An_empty_schedule_cannot_be_approved()
    {
        Assert.NotNull(ValidateForApproval([], []));
    }

    [Fact]
    public void A_valid_schedule_with_dependencies_can_be_approved()
    {
        var tasks = new[] { Task(1, "أ", D(10, 1), D(10, 10)), Task(2, "ب", D(10, 11), D(10, 20)) };
        Assert.Null(ValidateForApproval(tasks, [new Edge(1, 2)]));
    }

    [Fact]
    public void Approval_is_blocked_by_a_bad_task_a_foreign_dependency_or_a_cycle()
    {
        var bad = Task(1, "أ", D(10, 10), D(10, 1));
        Assert.Contains("أ", ValidateForApproval([bad], [])!);

        var ok = new[] { Task(1, "أ", D(10, 1), D(10, 5)), Task(2, "ب", D(10, 6), D(10, 9)) };
        Assert.NotNull(ValidateForApproval(ok, [new Edge(1, 99)]));
        Assert.NotNull(ValidateForApproval(ok, [new Edge(1, 2), new Edge(2, 1)]));
    }

    [Fact]
    public void The_creator_of_a_schedule_cannot_approve_it()
    {
        var draft = new ProjectSchedule { Status = ScheduleStatus.Draft, CreatedByUserId = 7 };
        Assert.Throws<InvalidOperationException>(() => EnsureCanApprove(ViewerOf(7), draft));
        EnsureCanApprove(ViewerOf(8), draft);
        Assert.Throws<InvalidOperationException>(() => EnsureCanApprove(ViewerOf(8), new ProjectSchedule { Status = ScheduleStatus.Approved, CreatedByUserId = 7 }));
    }

    [Fact]
    public void Original_dates_are_fixed_once_and_never_overwritten()
    {
        var t = Task(1, "أ", D(10, 1), D(10, 10));
        SnapshotOriginals([t]);
        Assert.Equal((D(10, 1), D(10, 10)), (t.OriginalStart, t.OriginalEnd));

        t.PlannedEnd = D(10, 25);                 // تمديد لاحق
        SnapshotOriginals([t]);
        Assert.Equal(D(10, 10), t.OriginalEnd);   // الأصل ثابت
    }

    [Fact]
    public void A_revision_copies_tasks_with_their_baseline_and_status_but_not_ids_or_history()
    {
        var a = Task(11, "أ", D(10, 1), D(10, 10), 2, 40, ScheduleTaskStatus.InProgress);
        a.OriginalStart = D(9, 25); a.OriginalEnd = D(10, 5); a.IsCritical = true; a.SortOrder = 2; a.AssigneeUserId = 9;
        a.ProgressHistory.Add(new ScheduleProgressEntry { Progress = 40 });
        var b = Task(12, "ب", D(10, 11), D(10, 20)); b.SortOrder = 1;

        var copies = CloneForRevision([a, b]);

        Assert.Equal([12, 11], copies.Select(c => c.OldId));              // بترتيب العرض
        var copy = copies.Single(c => c.OldId == 11).Copy;
        Assert.Equal(0, copy.Id);
        Assert.Empty(copy.ProgressHistory);
        Assert.Equal((D(9, 25), D(10, 5)), (copy.OriginalStart, copy.OriginalEnd));
        Assert.True(copy.IsCritical);
        Assert.Equal((2m, 40, ScheduleTaskStatus.InProgress, 9), (copy.Weight, copy.Progress, copy.Status, copy.AssigneeUserId));
    }

    // ───────── أمر المباشرة ─────────

    [Fact]
    public void The_start_order_is_validated()
    {
        Assert.Null(ValidateStartOrder("م/12", D(10, 1), D(10, 5), 120, 90, 1500000));
        Assert.NotNull(ValidateStartOrder("", D(10, 1), D(10, 5), 120, 90, null));
        Assert.NotNull(ValidateStartOrder("م/12", D(10, 5), D(10, 1), 120, 90, null));   // مباشرة قبل الأمر
        Assert.NotNull(ValidateStartOrder("م/12", D(10, 1), D(10, 5), 0, 90, null));
        Assert.NotNull(ValidateStartOrder("م/12", D(10, 1), D(10, 5), 120, 40000, null));
        Assert.NotNull(ValidateStartOrder("م/12", D(10, 1), D(10, 5), 120, 90, -1));
    }

    // ───────── التمديد ─────────

    private static (Dictionary<int, ScheduleTask> Tasks, Edge[] Edges) Chain()
    {
        var tasks = ById(
            Task(1, "حفر", D(10, 1), D(10, 10)),
            Task(2, "صب", D(10, 11), D(10, 20)),
            Task(3, "تشطيب", D(10, 21), D(10, 31)),
            Task(4, "توريد", D(10, 1), D(10, 5), progress: 100, status: ScheduleTaskStatus.Completed));
        return (tasks, [new Edge(1, 2), new Edge(2, 3), new Edge(4, 2)]);
    }

    [Fact]
    public void A_group_extension_moves_only_the_ends_of_the_chosen_tasks()
    {
        var (tasks, _) = Chain();
        var lines = new[] { new ExtensionLine(1, 5), new ExtensionLine(2, 5) };

        Assert.Null(ValidateExtensionLines(lines, tasks, ExtensionMode.Group));
        var preview = Preview(lines, tasks, ExtensionDayCount.Calendar, NoHolidays);

        Assert.Equal((D(10, 1), D(10, 10), D(10, 1), D(10, 15)), (preview[0].OldStart, preview[0].OldEnd, preview[0].NewStart, preview[0].NewEnd));
        Assert.Equal((D(10, 11), D(10, 25)), (preview[1].NewStart, preview[1].NewEnd));
        Assert.Equal(D(10, 10), tasks[1].PlannedEnd);      // المعاينة لا تغيّر البنود
    }

    [Fact]
    public void A_shifted_successor_moves_both_dates_and_keeps_its_length()
    {
        var (tasks, _) = Chain();
        var preview = Preview([new ExtensionLine(3, 7, MovesStart: true)], tasks, ExtensionDayCount.Calendar, NoHolidays);
        Assert.Equal((D(10, 28), D(11, 7)), (preview[0].NewStart, preview[0].NewEnd));
        Assert.Equal(CalendarDays(tasks[3].PlannedStart, tasks[3].PlannedEnd), CalendarDays(preview[0].NewStart, preview[0].NewEnd));
    }

    [Fact]
    public void Independent_extension_allows_a_different_number_of_days_per_task()
    {
        var (tasks, _) = Chain();
        var lines = new[] { new ExtensionLine(1, 3), new ExtensionLine(2, 9) };
        Assert.Null(ValidateExtensionLines(lines, tasks, ExtensionMode.Single));
        Assert.NotNull(ValidateExtensionLines(lines, tasks, ExtensionMode.Group));   // الجماعي بعدد واحد
    }

    [Fact]
    public void Extension_lines_are_validated()
    {
        var (tasks, _) = Chain();
        Assert.NotNull(ValidateExtensionLines([], tasks, ExtensionMode.Group));
        Assert.NotNull(ValidateExtensionLines([new ExtensionLine(1, 5), new ExtensionLine(1, 5)], tasks, ExtensionMode.Single));     // مكرر
        Assert.NotNull(ValidateExtensionLines([new ExtensionLine(99, 5)], tasks, ExtensionMode.Group));                              // ليس في الجدول
        Assert.NotNull(ValidateExtensionLines([new ExtensionLine(4, 5)], tasks, ExtensionMode.Group));                               // مكتمل
        Assert.NotNull(ValidateExtensionLines([new ExtensionLine(1, 0)], tasks, ExtensionMode.Group));
        Assert.NotNull(ValidateExtensionLines([new ExtensionLine(1, MaxExtensionDays + 1)], tasks, ExtensionMode.Group));
    }

    [Fact]
    public void Working_day_extension_uses_the_holiday_calendar()
    {
        var (tasks, _) = Chain();
        // نهاية «حفر» الجمعة؟ 10/10 سبت. خمسة أيام عمل: الأحد 11، الإثنين 12، الثلاثاء 13، الأربعاء 14، الخميس 15
        var preview = Preview([new ExtensionLine(1, 5)], tasks, ExtensionDayCount.WorkingDays, NoHolidays);
        Assert.Equal(D(10, 15), preview[0].NewEnd);
    }

    [Fact]
    public void Successors_are_suggested_for_review_but_never_the_chosen_or_the_completed()
    {
        var (tasks, edges) = Chain();
        var suggested = SuggestedSuccessors([1], edges, tasks);
        Assert.Equal([2, 3], suggested);                                  // لا البند المختار ولا «توريد» المكتمل
        Assert.Equal([3], SuggestedSuccessors([1, 2], edges, tasks));      // «صب» اختير فلا يُقترح
        Assert.Empty(SuggestedSuccessors([3], edges, tasks));
    }

    [Fact]
    public void Conflicts_that_remain_after_the_preview_are_reported()
    {
        var (tasks, edges) = Chain();
        // مُدَّد «حفر» 5 أيام (ينتهي 15) و«صب» يبدأ 11: تعارض باقٍ
        var preview = Preview([new ExtensionLine(1, 5)], tasks, ExtensionDayCount.Calendar, NoHolidays);
        var conflicts = PreviewConflicts(preview, tasks, edges.Where(e => e.PredecessorId == 1));
        Assert.Equal([new Edge(1, 2)], conflicts);

        // إزاحة «صب» أيضاً تزيل التعارض
        var both = Preview([new ExtensionLine(1, 5), new ExtensionLine(2, 5, MovesStart: true)], tasks, ExtensionDayCount.Calendar, NoHolidays);
        Assert.DoesNotContain(new Edge(1, 2), PreviewConflicts(both, tasks, edges));
    }

    [Fact]
    public void A_requester_cannot_decide_their_own_extension_and_only_a_submitted_one_is_decided()
    {
        var submitted = new ScheduleExtension { Status = ExtensionStatus.Submitted, RequestedByUserId = 4 };
        Assert.Throws<InvalidOperationException>(() => EnsureCanDecideExtension(ViewerOf(4), submitted));
        EnsureCanDecideExtension(ViewerOf(5), submitted);
        Assert.Throws<InvalidOperationException>(() => EnsureCanDecideExtension(ViewerOf(5), new ScheduleExtension { Status = ExtensionStatus.Draft, RequestedByUserId = 4 }));
        Assert.Throws<InvalidOperationException>(() => EnsureCanDecideExtension(ViewerOf(5), new ScheduleExtension { Status = ExtensionStatus.Approved, RequestedByUserId = 4 }));
    }

    [Fact]
    public void An_extension_prepared_on_old_dates_is_refused_after_the_dates_changed()
    {
        var (tasks, _) = Chain();
        var items = new[] { new ScheduleExtensionItem { TaskId = 1, OldStart = D(10, 1), OldEnd = D(10, 10), NewStart = D(10, 1), NewEnd = D(10, 15) } };
        Assert.Null(StalePreviewProblem(items, tasks));

        tasks[1].PlannedEnd = D(10, 12);        // مدّده قرار آخر
        Assert.Contains("حفر", StalePreviewProblem(items, tasks)!);
        Assert.NotNull(StalePreviewProblem([new ScheduleExtensionItem { TaskId = 77, OldStart = D(10, 1), OldEnd = D(10, 2) }], tasks));
    }

    [Fact]
    public void Applying_an_extension_changes_current_dates_and_never_the_original_ones()
    {
        var (tasks, _) = Chain();
        SnapshotOriginals(tasks.Values);
        var items = new[]
        {
            new ScheduleExtensionItem { TaskId = 1, OldStart = D(10, 1), OldEnd = D(10, 10), NewStart = D(10, 1), NewEnd = D(10, 15) },
            new ScheduleExtensionItem { TaskId = 2, OldStart = D(10, 11), OldEnd = D(10, 20), NewStart = D(10, 16), NewEnd = D(10, 25) }
        };

        ApplyExtension(items, tasks);

        Assert.Equal((D(10, 1), D(10, 15)), (tasks[1].PlannedStart, tasks[1].PlannedEnd));
        Assert.Equal((D(10, 16), D(10, 25)), (tasks[2].PlannedStart, tasks[2].PlannedEnd));
        Assert.Equal((D(10, 1), D(10, 10)), (tasks[1].OriginalStart, tasks[1].OriginalEnd));
        Assert.Equal((D(10, 11), D(10, 20)), (tasks[2].OriginalStart, tasks[2].OriginalEnd));
        Assert.Equal(5, ExtraDays(tasks[1]));
        Assert.Equal(5, ExtraDays(tasks[2]));
        Assert.Equal(D(10, 31), tasks[3].PlannedEnd);                 // ما لم يُمدَّد لم يتغير
    }

    // ───────── التأخير والتبرير ─────────

    [Fact]
    public void A_completed_task_has_no_delay_and_a_future_discovery_date_is_refused()
    {
        var open = Task(1, "أ", D(10, 1), D(10, 10));
        var done = Task(2, "ب", D(10, 1), D(10, 10), progress: 100, status: ScheduleTaskStatus.Completed);
        var today = D(10, 20);

        Assert.Null(ValidateDelayRecord(open, DelayKind.Justified, today, null, null, today));
        Assert.NotNull(ValidateDelayRecord(done, DelayKind.Unjustified, today, "سبب", null, today));
        Assert.NotNull(ValidateDelayRecord(open, DelayKind.Unjustified, D(10, 21), "سبب", null, today));
    }

    [Fact]
    public void An_unjustified_delay_needs_a_note_or_an_action_on_record()
    {
        var open = Task(1, "أ", D(10, 1), D(10, 10));
        Assert.NotNull(ValidateDelayRecord(open, DelayKind.Unjustified, D(10, 11), " ", "", D(10, 12)));
        Assert.Null(ValidateDelayRecord(open, DelayKind.Unjustified, D(10, 11), "", "أُرسل إنذار للمقاول", D(10, 12)));
    }

    [Fact]
    public void A_justification_needs_reason_authority_date_and_the_approval_document()
    {
        var today = D(10, 20);
        Assert.Null(ValidateJustification("تأخر التوريد", "رئيس الفرع", D(10, 15), 41, today));
        Assert.NotNull(ValidateJustification("", "رئيس الفرع", D(10, 15), 41, today));
        Assert.NotNull(ValidateJustification("سبب", " ", D(10, 15), 41, today));
        Assert.NotNull(ValidateJustification("سبب", "رئيس الفرع", null, 41, today));
        Assert.NotNull(ValidateJustification("سبب", "رئيس الفرع", D(10, 21), 41, today));    // موافقة مستقبلية
        Assert.NotNull(ValidateJustification("سبب", "رئيس الفرع", D(10, 15), null, today));  // بلا مستند: لا يُقبل
    }

    [Fact]
    public void A_justification_is_decided_by_someone_else_and_a_rejection_needs_a_reason()
    {
        var delay = new ScheduleDelay { Kind = DelayKind.Justified, Status = DelayStatus.Submitted, RecordedByUserId = 3 };
        Assert.Throws<InvalidOperationException>(() => EnsureCanDecideJustification(ViewerOf(3), delay, true, null));
        EnsureCanDecideJustification(ViewerOf(4), delay, true, null);
        Assert.Throws<InvalidOperationException>(() => EnsureCanDecideJustification(ViewerOf(4), delay, false, "  "));
        EnsureCanDecideJustification(ViewerOf(4), delay, false, "المستند غير كافٍ");
    }

    [Theory]
    [InlineData(DelayKind.Unjustified, DelayStatus.Recorded)]
    [InlineData(DelayKind.Justified, DelayStatus.Recorded)]      // مسودة التبرير لم تُرفع
    [InlineData(DelayKind.Justified, DelayStatus.Approved)]
    [InlineData(DelayKind.Justified, DelayStatus.Rejected)]
    public void Only_a_submitted_justified_delay_can_be_decided(DelayKind kind, DelayStatus status) =>
        Assert.Throws<InvalidOperationException>(() =>
            EnsureCanDecideJustification(ViewerOf(9), new ScheduleDelay { Kind = kind, Status = status, RecordedByUserId = 1 }, true, null));

    [Fact]
    public void Every_status_and_kind_has_an_arabic_name()
    {
        foreach (var s in Enum.GetValues<ScheduleStatus>()) Assert.NotEqual(s.ToString(), StatusAr(s));
        foreach (var s in Enum.GetValues<ScheduleTaskStatus>()) Assert.NotEqual(s.ToString(), TaskStatusAr(s));
        foreach (var s in Enum.GetValues<DelayStatus>()) Assert.NotEqual(s.ToString(), DelayStatusAr(s));
        foreach (var s in Enum.GetValues<ExtensionStatus>()) Assert.NotEqual(s.ToString(), ExtensionStatusAr(s));
        foreach (var k in Enum.GetValues<DelayKind>()) Assert.NotEqual(k.ToString(), DelayKindAr(k));
        foreach (var c in Enum.GetValues<ExtensionDayCount>()) Assert.NotEqual(c.ToString(), DayCountAr(c));
    }

    [Fact]
    public void Schedule_permissions_are_registered_once_and_readable_by_the_any_of_policy()
    {
        string[] names =
        [
            AppPermissions.ViewProjectSchedule, AppPermissions.EditProjectStartOrder, AppPermissions.EditProjectSchedule, AppPermissions.ApproveProjectSchedule,
            AppPermissions.UpdateScheduleProgress, AppPermissions.ClassifyScheduleTask, AppPermissions.RecordScheduleDelay, AppPermissions.ApproveDelayJustification,
            AppPermissions.RequestScheduleExtension, AppPermissions.ApproveScheduleExtension
        ];
        var registered = AppPermissions.All.Select(p => p.Name).ToList();
        foreach (var name in names)
        {
            Assert.Single(registered, name);
            Assert.Contains(name, AppPermissions.AnyOf["AnyScheduleView"]);
        }
    }
}
