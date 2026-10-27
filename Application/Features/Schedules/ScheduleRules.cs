using Application.Common;
using Application.Features.Vacations;
using Domain.Entities;

namespace Application.Features.Schedules
{
    /// <summary>
    /// قواعد الجدول الزمني والتأخير والتمديد (المرحلة 1، من تحليل الجنوبية) — مصدر واحد صرف بلا قاعدة بيانات:
    /// - النظام ينبّه ويسجّل ولا يقرّر: مرور تاريخ البند «مرشَّح للتأخر» فقط، والتأخير يسجّله مخوَّل، والتبرير والتمديد يعتمدهما غير من سجّلهما.
    /// - التواريخ الأصلية تُثبَّت مرة عند الاعتماد ولا تتغير؛ الحالية تتغير بتمديد معتمد فقط.
    /// - وزن البند يدخله المسؤول يدوياً، ونسبة المشروع متوسط مرجَّح بهذه الأوزان.
    /// - أيام التمديد تقويمية أو أيام عمل (بلا الجمعة والعطل الرسمية، بتقويم الإجازات نفسه VacationCalendar).
    /// </summary>
    public static class ScheduleRules
    {
        public const int MaxTasks = 1000;
        public const int MaxExtensionDays = 365;
        public const decimal MaxWeight = 1_000_000m;

        public sealed record Edge(int PredecessorId, int SuccessorId);

        // ───────── الأسماء ─────────

        public static string StatusAr(ScheduleStatus s) => s switch
        {
            ScheduleStatus.Draft => "مسودة", ScheduleStatus.Approved => "معتمد", _ => "سابق"
        };

        public static string TaskStatusAr(ScheduleTaskStatus s) => s switch
        {
            ScheduleTaskStatus.NotStarted => "لم يبدأ", ScheduleTaskStatus.InProgress => "قيد التنفيذ",
            ScheduleTaskStatus.Completed => "مكتمل", _ => "موقوف"
        };

        public static string DelayKindAr(DelayKind k) => k == DelayKind.Justified ? "مبرَّر" : "غير مبرَّر";

        public static string DelayStatusAr(DelayStatus s) => s switch
        {
            DelayStatus.Recorded => "مسجَّل", DelayStatus.Submitted => "بانتظار اعتماد التبرير", DelayStatus.Approved => "اعتُمد تبريره",
            DelayStatus.Rejected => "رُفض تبريره", _ => "انتهى أثره"
        };

        public static string ExtensionStatusAr(ExtensionStatus s) => s switch
        {
            ExtensionStatus.Draft => "مسودة", ExtensionStatus.Submitted => "بانتظار الاعتماد", ExtensionStatus.Approved => "معتمد", _ => "مرفوض"
        };

        public static string DayCountAr(ExtensionDayCount c) => c == ExtensionDayCount.WorkingDays ? "أيام عمل" : "أيام تقويمية";

        // ───────── التواريخ ─────────

        /// <summary>عدد الأيام التقويمية شاملاً طرفي المدة</summary>
        public static int CalendarDays(DateTime start, DateTime end) => (end.Date - start.Date).Days + 1;

        /// <summary>
        /// يزيح تاريخاً بعدد أيام: تقويمية، أو أيام عمل فلا يُحسب منها الجمعة ولا العطل الرسمية
        /// (الناتج يوم عمل دائماً في وضع العمل، فالإزاحة بـ 1 من خميس تعطي السبت).
        /// </summary>
        public static DateTime AddDays(DateTime date, int days, ExtensionDayCount mode, IReadOnlySet<DateTime> holidays)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), "عدد الأيام لا يكون سالباً");
            if (mode == ExtensionDayCount.Calendar) return date.Date.AddDays(days);

            var d = date.Date;
            for (var left = days; left > 0;)
            {
                d = d.AddDays(1);
                if (VacationCalendar.IsWorkingDay(d, holidays)) left--;
            }
            return d;
        }

        // ───────── البنود ─────────

        /// <summary>مشكلة بيانات البند (للرسالة العربية)، أو null</summary>
        public static string? ValidateTask(string? name, DateTime start, DateTime end, decimal weight)
        {
            if (string.IsNullOrWhiteSpace(name)) return "اسم بند العمل مطلوب";
            if (name.Trim().Length > 300) return "اسم البند لا يتجاوز 300 حرف";
            if (end.Date < start.Date) return "تاريخ نهاية البند قبل بدايته";
            if (weight <= 0) return "وزن البند يجب أن يكون أكبر من صفر";
            if (weight > MaxWeight) return "وزن البند كبير جداً";
            return null;
        }

        /// <summary>
        /// حالة البند ونسبته وتاريخاه الفعليان متسقة: «مكتمل» = 100%، و«لم يبدأ» = 0%، و100% = مكتمل، والتواريخ الفعلية لا تقع في المستقبل.
        /// </summary>
        public static string? ValidateProgress(ScheduleTaskStatus status, int progress, DateTime? actualStart, DateTime? actualEnd, DateTime today)
        {
            if (progress is < 0 or > 100) return "نسبة الإنجاز بين 0 و100";
            if (status == ScheduleTaskStatus.Completed && progress != 100) return "البند المكتمل نسبة إنجازه 100%";
            if (progress == 100 && status != ScheduleTaskStatus.Completed) return "نسبة 100% تعني أن البند مكتمل: غيّر حالته إلى «مكتمل»";
            if (status == ScheduleTaskStatus.NotStarted && progress > 0) return "بند لم يبدأ لا تكون له نسبة إنجاز";
            if (actualStart?.Date > today.Date || actualEnd?.Date > today.Date) return "التاريخ الفعلي لا يكون في المستقبل";
            if (actualStart != null && actualEnd != null && actualEnd.Value.Date < actualStart.Value.Date) return "نهاية التنفيذ الفعلية قبل بدايته";
            return null;
        }

        /// <summary>
        /// يكمل التواريخ الفعلية من الحالة عند تحديثها: بدء التنفيذ يسجّل البداية الفعلية بتاريخ اليوم إن لم تُدخَل،
        /// والاكتمال يسجّل النهاية (وتُكمَّل البداية بها إن لم تُسجَّل). ولا يُمحى تاريخ فعلي سُجّل قبلاً.
        /// </summary>
        public static (DateTime? Start, DateTime? End) CompleteActualDates(ScheduleTaskStatus status, DateTime? actualStart, DateTime? actualEnd, DateTime today)
        {
            if (status is ScheduleTaskStatus.InProgress or ScheduleTaskStatus.Completed or ScheduleTaskStatus.Suspended)
                actualStart ??= today.Date;
            if (status == ScheduleTaskStatus.Completed)
                actualEnd ??= today.Date;
            if (status != ScheduleTaskStatus.Completed) actualEnd = null;
            if (status == ScheduleTaskStatus.NotStarted) actualStart = null;
            return (actualStart, actualEnd);
        }

        // ───────── النسب ─────────

        /// <summary>نسبة المشروع الفعلية (0–100): متوسط نسب البنود مرجَّحاً بأوزانها اليدوية</summary>
        public static decimal ActualProgress(IEnumerable<ScheduleTask> tasks)
        {
            var list = tasks.ToList();
            var total = list.Sum(t => t.Weight);
            return total <= 0 ? 0 : Math.Round(list.Sum(t => t.Progress * t.Weight) / total, 1);
        }

        /// <summary>
        /// نسبة المشروع المخططة (0–100) حتى تاريخ معيّن، بنفس الأوزان: كل بند قبل بدايته 0، وبعد نهايته 100، وبينهما بنسبة الأيام المنقضية.
        /// useOriginal = على خط الأساس (التواريخ الأصلية إن وُجدت) بدل التواريخ الحالية بعد التمديد.
        /// </summary>
        public static decimal PlannedProgress(IEnumerable<ScheduleTask> tasks, DateTime asOf, bool useOriginal = false)
        {
            var list = tasks.ToList();
            var total = list.Sum(t => t.Weight);
            if (total <= 0) return 0;

            decimal sum = 0;
            foreach (var t in list)
            {
                var start = (useOriginal ? t.OriginalStart : null) ?? t.PlannedStart;
                var end = (useOriginal ? t.OriginalEnd : null) ?? t.PlannedEnd;
                decimal fraction;
                if (asOf.Date < start.Date) fraction = 0;
                else if (asOf.Date >= end.Date) fraction = 1;
                else fraction = (decimal)((asOf.Date - start.Date).Days + 1) / CalendarDays(start, end);
                sum += fraction * 100 * t.Weight;
            }
            return Math.Round(sum / total, 1);
        }

        // ───────── مرشَّح التأخر ─────────

        /// <summary>
        /// مرشَّح للتأخر: تجاوز نهايته الحالية ولم يكتمل (ولم يُوقف). ليس تأخيراً مسجَّلاً: مرور التاريخ وحده ليس دليلاً نهائياً،
        /// فيُنبَّه المخوَّلون ليراجعوا حالة العمل ثم يسجّلوا التأخير أو يحدّثوا الحالة.
        /// </summary>
        public static bool IsOverdueCandidate(ScheduleTask task, DateTime today) =>
            task.Status is ScheduleTaskStatus.NotStarted or ScheduleTaskStatus.InProgress && task.PlannedEnd.Date < today.Date;

        /// <summary>المدة الإضافية المعتمدة بعد التمديد: الفرق بين نهايته الحالية والأصلية (0 إن لم يمدَّد أو لم يُعتمد جدوله)</summary>
        public static int ExtraDays(ScheduleTask task) =>
            task.OriginalEnd is DateTime original ? Math.Max(0, (task.PlannedEnd.Date - original.Date).Days) : 0;

        // ───────── التبعية ─────────

        /// <summary>هل إضافة «predecessor ← successor» تُنشئ حلقة؟ (أي هل يصل successor إلى predecessor بالتبعيات الحالية)</summary>
        public static bool WouldCreateCycle(IEnumerable<Edge> edges, int predecessorId, int successorId)
        {
            if (predecessorId == successorId) return true;
            return Reachable(edges.ToLookup(e => e.PredecessorId, e => e.SuccessorId), successorId).Contains(predecessorId);
        }

        /// <summary>كل البنود اللاحقة (مباشرة وغير مباشرة) لمجموعة بنود</summary>
        public static HashSet<int> TransitiveSuccessors(IEnumerable<Edge> edges, IEnumerable<int> from)
        {
            var next = edges.ToLookup(e => e.PredecessorId, e => e.SuccessorId);
            var all = new HashSet<int>();
            foreach (var id in from) all.UnionWith(Reachable(next, id));
            return all;
        }

        private static HashSet<int> Reachable(ILookup<int, int> next, int from)
        {
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(from);
            while (stack.Count > 0)
                foreach (var n in next[stack.Pop()])
                    if (seen.Add(n)) stack.Push(n);
            return seen;
        }

        /// <summary>تعارضات «نهاية ← بداية»: لاحق يبدأ في يوم نهاية سابقه أو قبله (تحذير لا منع)</summary>
        public static List<Edge> DependencyConflicts(IReadOnlyDictionary<int, ScheduleTask> tasks, IEnumerable<Edge> edges) =>
            edges.Where(e => tasks.TryGetValue(e.PredecessorId, out var p) && tasks.TryGetValue(e.SuccessorId, out var s)
                             && s.PlannedStart.Date <= p.PlannedEnd.Date).ToList();

        // ───────── اعتماد الإصدار ─────────

        /// <summary>مشكلة تمنع اعتماد المسودة (للرسالة العربية)، أو null. التعارضات الزمنية بين السوابق واللواحق تحذير فقط.</summary>
        public static string? ValidateForApproval(IReadOnlyList<ScheduleTask> tasks, IReadOnlyList<Edge> edges)
        {
            if (tasks.Count == 0) return "الجدول بلا بنود";
            foreach (var t in tasks)
                if (ValidateTask(t.Name, t.PlannedStart, t.PlannedEnd, t.Weight) is string problem)
                    return $"البند «{t.Name}»: {problem}";

            var ids = tasks.Select(t => t.Id).ToHashSet();
            if (edges.Any(e => !ids.Contains(e.PredecessorId) || !ids.Contains(e.SuccessorId))) return "تبعية تشير إلى بند خارج هذا الجدول";
            var accumulated = new List<Edge>();
            foreach (var e in edges)
            {
                if (WouldCreateCycle(accumulated, e.PredecessorId, e.SuccessorId)) return "في التبعيات حلقة: بند يعتمد على نفسه بشكل غير مباشر";
                accumulated.Add(e);
            }
            return null;
        }

        /// <summary>اعتماد المسودة: من أنشأها لا يعتمدها (فصل الإعداد عن الاعتماد)</summary>
        public static void EnsureCanApprove(Viewer viewer, ProjectSchedule schedule)
        {
            if (schedule.Status != ScheduleStatus.Draft) throw new InvalidOperationException("هذا الإصدار ليس مسودة");
            if (schedule.CreatedByUserId == viewer.Id)
                throw new InvalidOperationException("من أعدّ الجدول لا يعتمده — يعتمده موظف آخر يملك صلاحية الاعتماد");
        }

        /// <summary>يثبّت التواريخ الأصلية للبنود التي لا أصل لها بعد (لا يغيّر أصلاً مثبّتاً)</summary>
        public static void SnapshotOriginals(IEnumerable<ScheduleTask> tasks)
        {
            foreach (var t in tasks)
            {
                t.OriginalStart ??= t.PlannedStart.Date;
                t.OriginalEnd ??= t.PlannedEnd.Date;
            }
        }

        /// <summary>
        /// نسخ بنود إصدار إلى مسودة جديدة (بلا معرّفات): التواريخ الأصلية تنتقل معها فلا يضيع خط الأساس،
        /// وتبقى الحالة والنسبة والأوزان والتصنيف. سجل التحديثات لا يُنسخ.
        /// </summary>
        public static List<(int OldId, ScheduleTask Copy)> CloneForRevision(IEnumerable<ScheduleTask> source) =>
            source.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).Select(t => (t.Id, new ScheduleTask
            {
                SortOrder = t.SortOrder, Code = t.Code, Name = t.Name,
                PlannedStart = t.PlannedStart, PlannedEnd = t.PlannedEnd, OriginalStart = t.OriginalStart, OriginalEnd = t.OriginalEnd,
                Weight = t.Weight, IsCritical = t.IsCritical, Status = t.Status, Progress = t.Progress,
                ActualStart = t.ActualStart, ActualEnd = t.ActualEnd, Note = t.Note, AssigneeUserId = t.AssigneeUserId,
                OverdueNotifiedFor = t.OverdueNotifiedFor
            })).ToList();

        // ───────── أمر المباشرة ─────────

        public static string? ValidateStartOrder(string? number, DateTime orderDate, DateTime startDate, int completionDays, int executionDays, decimal? studyCost)
        {
            if (string.IsNullOrWhiteSpace(number)) return "رقم أمر المباشرة مطلوب";
            if (number.Trim().Length > 50) return "رقم الأمر لا يتجاوز 50 حرفاً";
            if (startDate.Date < orderDate.Date) return "تاريخ المباشرة قبل تاريخ الأمر";
            if (completionDays is < 1 or > 36500) return "مدة الإنجاز بالأيام بين 1 و36500";
            if (executionDays is < 1 or > 36500) return "زمن التنفيذ بالأيام بين 1 و36500";
            if (studyCost is < 0) return "تكلفة الدراسة لا تكون سالبة";
            return null;
        }

        // ───────── التمديد ─────────

        /// <summary>سطر في طلب التمديد: البند وعدد الأيام، وهل تُزاح بدايته أيضاً (لاحق تأثر بسابقه)</summary>
        public sealed record ExtensionLine(int TaskId, int Days, bool MovesStart = false);

        public sealed record ExtensionPreviewItem(int TaskId, string TaskName, int Days, bool MovesStart,
            DateTime OldStart, DateTime OldEnd, DateTime NewStart, DateTime NewEnd);

        /// <summary>مشكلة أسطر التمديد، أو null: بند واحد في طلب واحد مرة واحدة، وبند مكتمل لا يُمدَّد، وعدد الأيام بين 1 و365</summary>
        public static string? ValidateExtensionLines(IReadOnlyList<ExtensionLine> lines, IReadOnlyDictionary<int, ScheduleTask> tasks, ExtensionMode mode)
        {
            if (lines.Count == 0) return "اختر بنداً واحداً على الأقل";
            if (lines.GroupBy(l => l.TaskId).Any(g => g.Count() > 1)) return "بند مكرر في الطلب";
            foreach (var line in lines)
            {
                if (!tasks.TryGetValue(line.TaskId, out var task)) return "بند لا يتبع هذا الجدول";
                if (line.Days is < 1 or > MaxExtensionDays) return $"أيام التمديد للبند «{task.Name}» بين 1 و{MaxExtensionDays}";
                if (task.Status == ScheduleTaskStatus.Completed) return $"البند «{task.Name}» مكتمل ولا يُمدَّد";
            }
            // التمديد الجماعي بعدد أيام واحد لكل البنود (الإزاحات التابعة تأخذه نفسه)
            if (mode == ExtensionMode.Group && lines.Select(l => l.Days).Distinct().Count() > 1)
                return "التمديد الجماعي بعدد أيام واحد لكل البنود — استعمل التمديد المستقل لمدد مختلفة";
            return null;
        }

        /// <summary>
        /// معاينة نتيجة التمديد قبل الاعتماد: نهاية كل بند ممدود تتحرك بعدد أيامه، وبند «مُزاح» تتحرك بدايته ونهايته معاً.
        /// لا تُغيَّر البنود هنا.
        /// </summary>
        public static List<ExtensionPreviewItem> Preview(IReadOnlyList<ExtensionLine> lines, IReadOnlyDictionary<int, ScheduleTask> tasks,
            ExtensionDayCount count, IReadOnlySet<DateTime> holidays) =>
            lines.Select(l =>
            {
                var t = tasks[l.TaskId];
                var newEnd = AddDays(t.PlannedEnd, l.Days, count, holidays);
                var newStart = l.MovesStart ? AddDays(t.PlannedStart, l.Days, count, holidays) : t.PlannedStart.Date;
                return new ExtensionPreviewItem(t.Id, t.Name, l.Days, l.MovesStart, t.PlannedStart.Date, t.PlannedEnd.Date, newStart, newEnd);
            }).ToList();

        /// <summary>
        /// البنود اللاحقة التي لم تُختر وقد تتأثر بالتمديد (مقترحة للمراجعة فقط): لا تنتقل إليها الإزاحة إلا باختيار صريح من صاحب القرار.
        /// البند المكتمل لا يُقترح.
        /// </summary>
        public static List<int> SuggestedSuccessors(IEnumerable<int> selected, IEnumerable<Edge> edges, IReadOnlyDictionary<int, ScheduleTask> tasks)
        {
            var chosen = selected.ToHashSet();
            return TransitiveSuccessors(edges, chosen)
                .Where(id => !chosen.Contains(id) && tasks.TryGetValue(id, out var t) && t.Status != ScheduleTaskStatus.Completed)
                .OrderBy(id => tasks[id].PlannedStart).ThenBy(id => id).ToList();
        }

        /// <summary>تعارضات تبقى بعد التمديد المقترح: لاحق يبدأ قبل نهاية سابقه الجديدة (تنبيه في المعاينة)</summary>
        public static List<Edge> PreviewConflicts(IReadOnlyList<ExtensionPreviewItem> preview, IReadOnlyDictionary<int, ScheduleTask> tasks, IEnumerable<Edge> edges)
        {
            var byTask = preview.ToDictionary(p => p.TaskId);
            DateTime Start(int id) => byTask.TryGetValue(id, out var p) ? p.NewStart : tasks[id].PlannedStart.Date;
            DateTime End(int id) => byTask.TryGetValue(id, out var p) ? p.NewEnd : tasks[id].PlannedEnd.Date;
            return edges.Where(e => tasks.ContainsKey(e.PredecessorId) && tasks.ContainsKey(e.SuccessorId)
                                    && byTask.ContainsKey(e.PredecessorId) && Start(e.SuccessorId) <= End(e.PredecessorId)).ToList();
        }

        /// <summary>اعتماد التمديد: صاحب الطلب لا يعتمده، ويُعتمد وهو مرفوع فقط</summary>
        public static void EnsureCanDecideExtension(Viewer viewer, ScheduleExtension extension)
        {
            if (extension.Status != ExtensionStatus.Submitted) throw new InvalidOperationException("هذا الطلب ليس بانتظار الاعتماد");
            if (extension.RequestedByUserId == viewer.Id)
                throw new InvalidOperationException("صاحب طلب التمديد لا يعتمده — يعتمده موظف آخر يملك صلاحية الاعتماد");
        }

        /// <summary>
        /// قبل التطبيق: تواريخ البنود الحالية ما زالت كما عُرضت في المعاينة؟ (بند غيّره قرار آخر بعد رفع الطلب يُوقف الاعتماد وتُعاد المعاينة)
        /// </summary>
        public static string? StalePreviewProblem(IEnumerable<ScheduleExtensionItem> items, IReadOnlyDictionary<int, ScheduleTask> tasks)
        {
            foreach (var item in items)
            {
                if (!tasks.TryGetValue(item.TaskId, out var task)) return "بند من الطلب لم يعد في الجدول المعتمد";
                if (task.PlannedStart.Date != item.OldStart.Date || task.PlannedEnd.Date != item.OldEnd.Date)
                    return $"تغيّرت تواريخ البند «{task.Name}» بعد إعداد الطلب — أعد إعداد الطلب ليُعاين من جديد";
            }
            return null;
        }

        /// <summary>يطبّق التمديد المعتمد على التواريخ الحالية دفعة واحدة؛ الأصلية لا تُمسّ أبداً</summary>
        public static void ApplyExtension(IEnumerable<ScheduleExtensionItem> items, IReadOnlyDictionary<int, ScheduleTask> tasks)
        {
            foreach (var item in items)
            {
                var task = tasks[item.TaskId];
                task.PlannedStart = item.NewStart.Date;
                task.PlannedEnd = item.NewEnd.Date;
            }
        }

        // ───────── التأخير ─────────

        /// <summary>تسجيل تأخير على بند: البند غير مكتمل، وتاريخ الاكتشاف ليس في المستقبل، وغير المبرَّر يوثّق ملاحظة أو إجراء</summary>
        public static string? ValidateDelayRecord(ScheduleTask task, DelayKind kind, DateTime discoveredAt, string? reason, string? actions, DateTime today)
        {
            if (task.Status == ScheduleTaskStatus.Completed) return "البند مكتمل: لا يُسجَّل عليه تأخير";
            if (discoveredAt.Date > today.Date) return "تاريخ اكتشاف التأخير لا يكون في المستقبل";
            if (kind == DelayKind.Unjustified && string.IsNullOrWhiteSpace(reason) && string.IsNullOrWhiteSpace(actions))
                return "وثّق ملاحظة أو إجراءً متخذاً للتأخير غير المبرَّر";
            return null;
        }

        /// <summary>
        /// رفع التبرير للاعتماد: السبب، والجهة التي وافقت وتاريخ موافقتها، ومستند الموافقة (وثيقة مشروع).
        /// رفع المرفق وحده لا يقبل التبرير؛ يبقى بانتظار من يملك صلاحية الاعتماد.
        /// </summary>
        public static string? ValidateJustification(string? reason, string? authority, DateTime? approvalDate, int? documentVersionId, DateTime today)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "سبب التأخير مطلوب";
            if (string.IsNullOrWhiteSpace(authority)) return "اذكر الجهة التي وافقت على التبرير";
            if (approvalDate is null) return "تاريخ الموافقة مطلوب";
            if (approvalDate.Value.Date > today.Date) return "تاريخ الموافقة لا يكون في المستقبل";
            if (documentVersionId is null) return "أرفق مستند الموافقة (من وثائق المشروع)";
            return null;
        }

        /// <summary>اعتماد التبرير أو رفضه: مبرَّر مرفوع فقط، وغير من سجّله، والرفض بسبب</summary>
        public static void EnsureCanDecideJustification(Viewer viewer, ScheduleDelay delay, bool approve, string? note)
        {
            if (delay.Kind != DelayKind.Justified || delay.Status != DelayStatus.Submitted)
                throw new InvalidOperationException("هذا التأخير ليس بانتظار اعتماد تبرير");
            if (delay.RecordedByUserId == viewer.Id)
                throw new InvalidOperationException("من سجّل التأخير لا يعتمد تبريره — يعتمده موظف آخر يملك صلاحية الاعتماد");
            if (!approve && string.IsNullOrWhiteSpace(note))
                throw new InvalidOperationException("اذكر سبب رفض التبرير");
        }
    }
}
