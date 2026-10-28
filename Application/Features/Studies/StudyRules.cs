using Application.Common;
using Application.Features.Projects;
using Domain.Entities;

namespace Application.Features.Studies
{
    /// <summary>
    /// الدراسات والمكاتب الفنية (المرحلة 3، 2026-10-28) — مصدر واحد للقواعد الصرفة (بلا قاعدة بيانات):
    /// - حدّ كل صلاحية هو حدّ رؤية المشروع (<see cref="ProjectRules.Visible"/>)؛ مرور الأمر بين الأقسام لا يفتحه لقسم كامل.
    /// - المراجع غير المُعدّ: من رفع المخرج للمراجعة أو رفع ملفه لا يراجعه، ولا استثناء لمدير النظام ([ADR-0008]).
    /// - «النظام ينبّه ولا يقرر»: التسلسل يمنع البدء قبل اعتماد السابق فقط عندما اختاره رئيس القسم، وتجاوز الموعد مرشّح تنبيه لا أكثر،
    ///   وحالة الأمر لا تنتقل إلا بإرسال أو قرار يسجّله مخوَّل.
    /// - الإصدار المعتمد من المخرج يُثبَّت لحظة الاعتماد ولا يُستبدل.
    /// </summary>
    public static class StudyRules
    {
        public const int MaxDeliverables = 30;
        public const int MaxTitleLength = 200;
        public const int MaxNoteLength = 1000;

        // ───────── الأسماء ─────────

        /// <summary>الرقم الداخلي STD-{سنة الإنشاء}-{id:D5} — يُحسب ولا يُخزَّن</summary>
        public static string Number(int id, DateTime createdAt) => $"STD-{createdAt.Year}-{id:D5}";

        public static string ModeAr(StudyMode mode) => mode == StudyMode.Parallel ? "بالتوازي" : "بالتسلسل";

        public static string StatusAr(StudyStatus status) => status switch
        {
            StudyStatus.Open => "قيد الإعداد",
            StudyStatus.SentToCenter => "أُرسلت للمركزية",
            StudyStatus.ApprovedByCenter => "اعتمدتها المركزية",
            StudyStatus.ReturnedByCenter => "أعادتها المركزية بملاحظات",
            StudyStatus.Cancelled => "ملغاة",
            _ => status.ToString()
        };

        public static string StateAr(DeliverableState state) => state switch
        {
            DeliverableState.Preparing => "قيد الإعداد",
            DeliverableState.InReview => "بانتظار المراجعة",
            DeliverableState.Approved => "معتمد",
            DeliverableState.Returned => "أُعيد للتعديل",
            _ => state.ToString()
        };

        public static string DecisionAr(CenterDecision decision) => decision switch
        {
            CenterDecision.Pending => "بانتظار القرار",
            CenterDecision.Approved => "اعتمدتها المركزية",
            CenterDecision.Notes => "أعادتها بملاحظات",
            _ => decision.ToString()
        };

        public static bool IsValidMode(int value) => Enum.IsDefined(typeof(StudyMode), value);
        public static bool IsValidDecision(int value) => value is (int)CenterDecision.Approved or (int)CenterDecision.Notes;

        // ───────── التحقق من المدخلات ─────────

        public static string? ValidateOrder(string? title, string? notes, DateTime? dueDate, DateTime today)
        {
            if (string.IsNullOrWhiteSpace(title)) return "عنوان أمر الدراسة مطلوب";
            if (title.Trim().Length > MaxTitleLength) return $"عنوان أمر الدراسة لا يتجاوز {MaxTitleLength} حرفاً";
            if ((notes ?? string.Empty).Length > 2000) return "الملاحظات لا تتجاوز 2000 حرف";
            if (dueDate is DateTime due && due.Date < today.Date.AddYears(-1)) return "موعد الدراسة بعيد في الماضي";
            return null;
        }

        /// <summary>
        /// المهندس المسؤول من مشاركي المشروع، نشط، ومن فرعه؛ والمكتب (إن حُدّد) من فرع المشروع، والمهندس فيه إن حُدّد الاثنان.
        /// </summary>
        public static string? ValidateAssignment(Project project, User? assignee, Office? office)
        {
            if (assignee != null)
            {
                if (!assignee.IsActive) return "حساب هذا الموظف معطّل";
                if (project.Members.All(m => m.UserId != assignee.Id)) return "المهندس المسؤول من مشاركي المشروع: أضفه مشاركاً أولاً";
            }
            if (office != null)
            {
                var officeBranch = office.Department?.BranchId;
                if (officeBranch != null && officeBranch != project.BranchId) return "المكتب من فرع آخر";
                if (assignee != null && assignee.OfficeId != office.Id) return "المهندس المسؤول ليس من هذا المكتب";
            }
            return null;
        }

        // ───────── الرؤية والحالة ─────────

        /// <summary>من لا يملك أي صلاحية دراسات لا يرى شيئاً، ثم حدّ المشروع نفسه</summary>
        public static void EnsureCanView(Viewer viewer, Project project)
        {
            if (!AppPermissions.AnyOf["AnyStudyView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض الدراسات");
            ProjectRules.EnsureView(viewer, project);
        }

        private static void Require(Viewer viewer, string permission, string action)
        {
            if (!viewer.IsSuperAdmin && !viewer.Has(permission))
                throw new UnauthorizedAccessException($"لا تملك صلاحية {action}");
        }

        /// <summary>تعديل أي شيء في أمر مفتوح فقط؛ غير ذلك رسالة تشرح حالته</summary>
        public static void EnsureOrderOpen(StudyOrder order)
        {
            if (order.Status != StudyStatus.Open)
                throw new InvalidOperationException($"أمر الدراسة «{StatusAr(order.Status)}»" +
                    (order.Status == StudyStatus.ReturnedByCenter ? ": أعد فتحه لجولة جديدة" : string.Empty));
        }

        public static void EnsureCanCreate(Viewer viewer, Project project)
        {
            Require(viewer, AppPermissions.CreateStudyOrder, "فتح أمر دراسة");
            ProjectRules.EnsureView(viewer, project);
            ProjectRules.EnsureOpen(project);
        }

        /// <summary>تعديل الأمر وإسناد مخرجاته (الأمر مفتوح والمشروع غير منتهٍ)</summary>
        public static void EnsureCanEdit(Viewer viewer, StudyOrder order)
        {
            Require(viewer, AppPermissions.EditStudyOrder, "تعديل أمر الدراسة");
            ProjectRules.EnsureView(viewer, order.Project);
            ProjectRules.EnsureOpen(order.Project);
            EnsureOrderOpen(order);
        }

        public static void EnsureCanAddDeliverable(StudyOrder order, int typeId)
        {
            if (order.Deliverables.Count >= MaxDeliverables) throw new InvalidOperationException($"بلغ الأمر الحد الأقصى للمخرجات ({MaxDeliverables})");
            if (order.Deliverables.Any(d => d.TypeId == typeId)) throw new InvalidOperationException("هذا النوع مضاف إلى الأمر مسبقاً");
        }

        public static void EnsureTypeUsable(DeliverableType type, int branchId)
        {
            if (type.BranchId != branchId) throw new InvalidOperationException("نوع المخرج من فرع آخر");
            if (!type.IsActive) throw new InvalidOperationException("نوع المخرج معطَّل");
        }

        /// <summary>مخرج لم يبدأ العمل عليه (بلا ملف ولا مراجعة) يجوز حذفه وتغيير نوعه</summary>
        public static bool IsUntouched(StudyDeliverable d) => d.DocumentId == null && d.State == DeliverableState.Preparing && d.SubmittedVersionId == null;

        public static void EnsureCanRemoveDeliverable(StudyDeliverable d)
        {
            if (!IsUntouched(d)) throw new InvalidOperationException("بدأ العمل على هذا المخرج: لا يُحذف");
        }

        // ───────── الترتيب ─────────

        private static IEnumerable<StudyDeliverable> InOrder(IEnumerable<StudyDeliverable> all) => all.OrderBy(d => d.SortOrder).ThenBy(d => d.Id);

        /// <summary>
        /// هل يجوز العمل على المخرج الآن؟ بالتوازي دائماً؛ بالتسلسل بعد اعتماد كل ما قبله في الترتيب.
        /// </summary>
        public static bool CanStart(StudyOrder order, StudyDeliverable deliverable)
        {
            if (order.Mode == StudyMode.Parallel) return true;
            foreach (var d in InOrder(order.Deliverables))
            {
                if (d.Id == deliverable.Id) return true;
                if (d.State != DeliverableState.Approved) return false;
            }
            return true;
        }

        /// <summary>المخرج الذي يسبقه ولم يُعتمد بعد (لرسالة المنع)؛ null إن جاز البدء</summary>
        public static StudyDeliverable? BlockedBy(StudyOrder order, StudyDeliverable deliverable)
        {
            if (order.Mode == StudyMode.Parallel) return null;
            foreach (var d in InOrder(order.Deliverables))
            {
                if (d.Id == deliverable.Id) return null;
                if (d.State != DeliverableState.Approved) return d;
            }
            return null;
        }

        // ───────── مسار المخرج: إعداد ← مراجعة ← معتمد/معاد ─────────

        /// <summary>الإعداد (رفع ملف أو رفع للمراجعة): للمهندس المسؤول، والمخرج في الإعداد أو معاد، والأمر مفتوح، ودوره في الترتيب</summary>
        public static void EnsureCanPrepare(Viewer viewer, StudyOrder order, StudyDeliverable d)
        {
            Require(viewer, AppPermissions.PrepareStudyDeliverable, "إعداد المخرجات");
            ProjectRules.EnsureView(viewer, order.Project);
            ProjectRules.EnsureOpen(order.Project);
            EnsureOrderOpen(order);
            if (!viewer.IsSuperAdmin && d.AssigneeUserId != viewer.Id)
                throw new UnauthorizedAccessException("هذا المخرج غير مسند إليك");
            if (d.State is DeliverableState.InReview) throw new InvalidOperationException("المخرج بانتظار المراجعة: لا يُعدَّل حتى يبتّ فيه المراجع");
            if (d.State is DeliverableState.Approved) throw new InvalidOperationException("المخرج معتمد: لا يُعدَّل (يعيده رئيس القسم لجولة جديدة)");
            if (BlockedBy(order, d) is { } before)
                throw new InvalidOperationException($"لا يبدأ هذا المخرج قبل اعتماد «{before.Type?.Name}» (الأمر بالتسلسل)");
        }

        /// <summary>
        /// الرفع للمراجعة: يلزم إصدار موجود، وبعد الإعادة يلزم إصدار أحدث من الذي أُعيد.
        /// </summary>
        public static void EnsureCanSubmitForReview(Viewer viewer, StudyOrder order, StudyDeliverable d, int? latestVersionId)
        {
            EnsureCanPrepare(viewer, order, d);
            if (d.AssigneeUserId == null) throw new InvalidOperationException("أسند المخرج إلى مهندس أولاً");
            if (latestVersionId == null) throw new InvalidOperationException("ارفع ملف المخرج قبل رفعه للمراجعة");
            if (d.State == DeliverableState.Returned && latestVersionId == d.SubmittedVersionId)
                throw new InvalidOperationException("أضف إصداراً جديداً من الملف بعد ملاحظات المراجع قبل رفعه من جديد");
        }

        /// <summary>
        /// المراجعة: لصاحب صلاحية المراجعة، والمخرج بانتظارها، وليس من رفعه ولا من رفع ملفه (حتى مدير النظام).
        /// الإعادة تشترط سبباً.
        /// </summary>
        public static void EnsureCanReview(Viewer viewer, StudyOrder order, StudyDeliverable d, int? submittedVersionUploaderId, bool approve, string? note)
        {
            Require(viewer, AppPermissions.ReviewStudyDeliverable, "مراجعة المخرجات");
            ProjectRules.EnsureView(viewer, order.Project);
            ProjectRules.EnsureOpen(order.Project);
            EnsureOrderOpen(order);
            if (d.State != DeliverableState.InReview) throw new InvalidOperationException("المخرج ليس بانتظار المراجعة");
            if (viewer.Id == d.SubmittedByUserId || viewer.Id == submittedVersionUploaderId)
                throw new UnauthorizedAccessException("لا تراجع مخرجاً أعددته أو رفعت ملفه: يراجعه غيرك");
            if (!approve && string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("اكتب سبب إعادة المخرج");
            if ((note ?? string.Empty).Length > MaxNoteLength) throw new InvalidOperationException($"السبب لا يتجاوز {MaxNoteLength} حرفاً");
        }

        /// <summary>
        /// إعادة مخرج معتمد إلى الإعداد لجولة جديدة (يقرر ذلك رئيس القسم بسبب مكتوب، والأمر مفتوح).
        /// الإصدار المعتمد السابق يبقى مثبَّتاً في سجله.
        /// </summary>
        public static void EnsureCanSendBack(Viewer viewer, StudyOrder order, StudyDeliverable d, string? note)
        {
            EnsureCanEdit(viewer, order);
            if (d.State != DeliverableState.Approved) throw new InvalidOperationException("لا يُعاد إلا مخرج معتمد");
            if (string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("اكتب سبب إعادة المخرج لجولة جديدة");
            if (note.Length > MaxNoteLength) throw new InvalidOperationException($"السبب لا يتجاوز {MaxNoteLength} حرفاً");
        }

        // ───────── الإرسال إلى المركزية وقرارها ─────────

        public static int NextRound(StudyOrder order) => order.Submissions.Count == 0 ? 1 : order.Submissions.Max(s => s.Round) + 1;

        /// <summary>الإرسال: للمخوَّل، والأمر مفتوح، وكل مخرجاته معتمدة (واحد على الأقل)</summary>
        public static void EnsureCanSend(Viewer viewer, StudyOrder order)
        {
            Require(viewer, AppPermissions.RecordStudySubmission, "تسجيل إرسال الدراسة");
            ProjectRules.EnsureView(viewer, order.Project);
            ProjectRules.EnsureOpen(order.Project);
            EnsureOrderOpen(order);
            if (order.Deliverables.Count == 0) throw new InvalidOperationException("أضف مخرجات إلى الأمر قبل إرساله");
            if (order.Deliverables.Any(d => d.State != DeliverableState.Approved))
                throw new InvalidOperationException("لا تُرسل الدراسة قبل اعتماد كل مخرجاتها");
            if (order.Submissions.Any(s => s.Decision == CenterDecision.Pending))
                throw new InvalidOperationException("بانتظار قرار الجولة السابقة");
        }

        /// <summary>تسجيل قرار المركزية على الجولة المعلّقة؛ «ملاحظات» تشترط نصها</summary>
        public static void EnsureCanDecide(Viewer viewer, StudyOrder order, StudySubmission submission, CenterDecision decision, string? note)
        {
            Require(viewer, AppPermissions.RecordStudySubmission, "تسجيل قرار المركزية");
            ProjectRules.EnsureView(viewer, order.Project);
            if (order.Status != StudyStatus.SentToCenter) throw new InvalidOperationException("الأمر ليس بانتظار قرار المركزية");
            if (submission.Decision != CenterDecision.Pending) throw new InvalidOperationException("سُجّل قرار هذه الجولة مسبقاً");
            if (decision is not (CenterDecision.Approved or CenterDecision.Notes)) throw new InvalidOperationException("القرار اعتماد أو ملاحظات");
            if (decision == CenterDecision.Notes && string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("اكتب ملاحظات المركزية");
            if ((note ?? string.Empty).Length > MaxNoteLength) throw new InvalidOperationException($"الملاحظات لا تتجاوز {MaxNoteLength} حرفاً");
        }

        public static StudyStatus StatusAfter(CenterDecision decision) =>
            decision == CenterDecision.Approved ? StudyStatus.ApprovedByCenter : StudyStatus.ReturnedByCenter;

        /// <summary>جولة جديدة بعد ملاحظات المركزية: يفتحها صاحب التعديل، ويعود الأمر مفتوحاً</summary>
        public static void EnsureCanReopen(Viewer viewer, StudyOrder order)
        {
            Require(viewer, AppPermissions.EditStudyOrder, "إعادة فتح الدراسة");
            ProjectRules.EnsureView(viewer, order.Project);
            ProjectRules.EnsureOpen(order.Project);
            if (order.Status != StudyStatus.ReturnedByCenter) throw new InvalidOperationException("لا يُعاد فتح إلا أمر أعادته المركزية بملاحظات");
        }

        /// <summary>الإلغاء بقرار: ليس بينما تنتظر المركزية (سجّل قرارها أولاً) ولا بعد اعتمادها</summary>
        public static void EnsureCanCancel(Viewer viewer, StudyOrder order)
        {
            Require(viewer, AppPermissions.EditStudyOrder, "إلغاء أمر الدراسة");
            ProjectRules.EnsureView(viewer, order.Project);
            ProjectRules.EnsureOpen(order.Project);
            if (order.Status is StudyStatus.SentToCenter) throw new InvalidOperationException("بانتظار قرار المركزية: سجّل قرارها قبل الإلغاء");
            if (order.Status is StudyStatus.ApprovedByCenter) throw new InvalidOperationException("اعتمدتها المركزية: لا تُلغى");
            if (order.Status is StudyStatus.Cancelled) throw new InvalidOperationException("الأمر ملغى مسبقاً");
        }

        // ───────── التنبيه (لا قرار) ─────────

        /// <summary>
        /// مرشّح للتنبيه: مخرج لم يُعتمد وتجاوز موعده في أمر مفتوح. التجاوز وحده لا يغيّر شيئاً.
        /// </summary>
        public static bool IsOverdueCandidate(StudyOrder order, StudyDeliverable d, DateTime today) =>
            order.Status == StudyStatus.Open && d.State != DeliverableState.Approved && d.DueDate is DateTime due && due.Date < today.Date;

        /// <summary>تنبيه واحد لكل موعد: لم يُنبَّه عنه، أو غُيّر الموعد بعد التنبيه</summary>
        public static bool NeedsOverdueNotice(StudyDeliverable d) =>
            d.DueDate is DateTime due && d.OverdueNotifiedFor?.Date != due.Date;

        /// <summary>نسبة المخرجات المعتمدة من الأمر (0–100)</summary>
        public static int ApprovedPercent(StudyOrder order) =>
            order.Deliverables.Count == 0 ? 0 : (int)Math.Floor(100.0 * order.Deliverables.Count(d => d.State == DeliverableState.Approved) / order.Deliverables.Count);
    }
}
