using Application.Common;
using Application.Features.Projects;
using Domain.Entities;

namespace Application.Features.Handover
{
    /// <summary>
    /// الاستلام والتسليم (المرحلة 4، 2026-10-31) — مصدر واحد للقواعد الصرفة (بلا قاعدة بيانات):
    /// - حدّ كل صلاحية هو حدّ رؤية المشروع (<see cref="ProjectRules.Visible"/>).
    /// - استلام أولي ونهائي سجلان منفصلان، لكل منهما طلب ولجنة وكشف ونتيجة؛ طلب جديد بعد ملاحظات = سجل جديد، وطلب واحد غير منتهٍ لكل نوع.
    /// - «بملاحظات» تشترط ملاحظة مسجَّلة واحدة على الأقل، و«بلا ملاحظات» لا تُسجَّل والملاحظات مفتوحة في المشروع (تُتابَع حتى تُغلق).
    /// - من عالج الملاحظة لا يتحقق منها ([ADR-0008]).
    /// - «النظام ينبّه ولا يقرر»: لا يُغلق المشروع آلياً لانتهاء تاريخ ولا لاكتمال الاستلام؛ <see cref="ClosureWarnings"/> تعرض ما لم يكتمل فقط.
    /// </summary>
    public static class HandoverRules
    {
        public const int MaxNotesPerRecord = 100;
        public const int MaxTextLength = 2000;

        // ───────── الأسماء ─────────

        /// <summary>الرقم الداخلي HND-{سنة الإنشاء}-{id:D5} — يُحسب ولا يُخزَّن</summary>
        public static string Number(int id, DateTime createdAt) => $"HND-{createdAt.Year}-{id:D5}";

        public static string KindAr(HandoverKind kind) => kind == HandoverKind.Final ? "استلام نهائي" : "استلام أولي";

        public static string StatusAr(HandoverStatus status) => status switch
        {
            HandoverStatus.Requested => "طلب بانتظار تشكيل اللجنة",
            HandoverStatus.CommitteeFormed => "لجنة مشكَّلة",
            HandoverStatus.Concluded => "منتهٍ",
            HandoverStatus.Cancelled => "ملغى",
            _ => status.ToString()
        };

        public static string ResultAr(HandoverResult? result) => result switch
        {
            HandoverResult.WithoutNotes => "بلا ملاحظات",
            HandoverResult.WithNotes => "بملاحظات",
            _ => string.Empty
        };

        public static string NoteStatusAr(HandoverNoteStatus status) => status switch
        {
            HandoverNoteStatus.Open => "مفتوحة",
            HandoverNoteStatus.Fixed => "عولجت بانتظار التحقق",
            HandoverNoteStatus.Verified => "مغلقة (تُحقِّق منها)",
            _ => status.ToString()
        };

        public static bool IsValidKind(int value) => Enum.IsDefined(typeof(HandoverKind), value);
        public static bool IsValidResult(int value) => Enum.IsDefined(typeof(HandoverResult), value);

        // ───────── الصلاحية والرؤية ─────────

        private static void Require(Viewer viewer, string permission, string action)
        {
            if (!viewer.IsSuperAdmin && !viewer.Has(permission))
                throw new UnauthorizedAccessException($"لا تملك صلاحية {action}");
        }

        /// <summary>من لا يملك أي صلاحية استلام لا يرى شيئاً، ثم حدّ المشروع نفسه</summary>
        public static void EnsureCanView(Viewer viewer, Project project)
        {
            if (!AppPermissions.AnyOf["AnyHandoverView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض محاضر الاستلام");
            ProjectRules.EnsureView(viewer, project);
        }

        /// <summary>تغيير سجل الاستلام: المشروع غير منتهٍ، والسجل لم يُنهَ ولم يُلغَ</summary>
        private static void EnsureLive(HandoverRecord record)
        {
            ProjectRules.EnsureOpen(record.Project);
            if (record.Status == HandoverStatus.Cancelled) throw new InvalidOperationException("طلب الاستلام ملغى");
        }

        // ───────── الطلب ─────────

        /// <summary>
        /// طلب استلام: للمخوَّل على مشروع يراه، وقد بلغ مرحلة التنفيذ (لا قبلها)، وليس لهذا النوع طلب أو لجنة غير منتهية، ولم يُسجَّل استلام نهائي
        /// بلا ملاحظات. العقد (إن حُدّد) من المشروع نفسه وساري أو منجز. <paramref name="existing"/> سجلات المشروع كلها.
        /// </summary>
        public static void EnsureCanRequest(Viewer viewer, Project project, HandoverKind kind, Contract? contract, IReadOnlyCollection<HandoverRecord> existing, string? note)
        {
            Require(viewer, AppPermissions.RequestHandover, "تسجيل طلب استلام");
            ProjectRules.EnsureView(viewer, project);
            ProjectRules.EnsureOpen(project);
            if (!IsValidKind((int)kind)) throw new InvalidOperationException("نوع الاستلام أولي أو نهائي");
            if (project.Stage is ProjectStage.Study or ProjectStage.Contracting)
                throw new InvalidOperationException("لا يُطلب الاستلام قبل بلوغ المشروع مرحلة التنفيذ");
            if (contract != null)
            {
                if (contract.ProjectId != project.Id) throw new InvalidOperationException("العقد من مشروع آخر");
                if (contract.Status is not (ContractStatus.Active or ContractStatus.Completed))
                    throw new InvalidOperationException("الاستلام على عقد ساري أو منجز");
            }
            if (existing.Any(h => h.Kind == kind && h.Status is HandoverStatus.Requested or HandoverStatus.CommitteeFormed))
                throw new InvalidOperationException($"لهذا المشروع طلب {KindAr(kind)} لم ينتهِ بعد");
            if (kind == HandoverKind.Final && existing.Any(IsFinalWithoutNotes))
                throw new InvalidOperationException("سُجّل استلام نهائي بلا ملاحظات لهذا المشروع");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("ملاحظة الطلب لا تتجاوز 1000 حرف");
        }

        public static bool IsFinalWithoutNotes(HandoverRecord h) =>
            h.Kind == HandoverKind.Final && h.Status == HandoverStatus.Concluded && h.Result == HandoverResult.WithoutNotes;

        // ───────── اللجنة والكشف والنتيجة ─────────

        /// <summary>تشكيل لجنة الاستلام: للمخوَّل، والطلب ما زال بانتظارها (لجنة واحدة لكل سجل)</summary>
        public static void EnsureCanFormCommittee(Viewer viewer, HandoverRecord record)
        {
            Require(viewer, AppPermissions.ManageHandover, "تشكيل لجنة الاستلام");
            ProjectRules.EnsureView(viewer, record.Project);
            EnsureLive(record);
            if (record.Status != HandoverStatus.Requested || record.Committee != null)
                throw new InvalidOperationException("شُكّلت لجنة لهذا الطلب مسبقاً");
        }

        /// <summary>تعديل أعضاء اللجنة وتوصياتها ما دام السجل لجنة مشكَّلة (قبل النتيجة)</summary>
        public static void EnsureCanEditCommittee(Viewer viewer, HandoverRecord record)
        {
            Require(viewer, AppPermissions.ManageHandover, "تعديل لجنة الاستلام");
            ProjectRules.EnsureView(viewer, record.Project);
            EnsureLive(record);
            if (record.Status != HandoverStatus.CommitteeFormed) throw new InvalidOperationException("اللجنة تُعدَّل قبل تسجيل النتيجة");
        }

        /// <summary>تاريخ الكشف: لا يسبق الطلب ولا يكون في المستقبل</summary>
        public static string? ValidateInspection(HandoverRecord record, DateTime inspectionDate, DateTime today)
        {
            if (inspectionDate.Date < record.RequestedAt.Date) return "تاريخ الكشف لا يسبق تاريخ الطلب";
            if (inspectionDate.Date > today.Date) return "تاريخ الكشف لا يكون في المستقبل";
            return null;
        }

        /// <summary>
        /// تسجيل النتيجة (ينتهي السجل): للمخوَّل، واللجنة مشكَّلة. «بملاحظات» تشترط ملاحظة واحدة على الأقل في السجل، و«بلا ملاحظات» لا تُقبل
        /// والسجل فيه ملاحظات أو في مشروعه ملاحظات لم تُغلق (<paramref name="otherOpenNotes"/> في سجلاته الأخرى).
        /// </summary>
        public static void EnsureCanConclude(Viewer viewer, HandoverRecord record, HandoverResult result, DateTime inspectionDate, int otherOpenNotes, string? resultNote, DateTime today)
        {
            Require(viewer, AppPermissions.ManageHandover, "تسجيل نتيجة الاستلام");
            ProjectRules.EnsureView(viewer, record.Project);
            EnsureLive(record);
            if (record.Status != HandoverStatus.CommitteeFormed) throw new InvalidOperationException("شكّل لجنة الاستلام أولاً");
            if (!IsValidResult((int)result)) throw new InvalidOperationException("النتيجة بلا ملاحظات أو بملاحظات");
            if (ValidateInspection(record, inspectionDate, today) is { } problem) throw new InvalidOperationException(problem);
            if ((resultNote ?? string.Empty).Length > MaxTextLength) throw new InvalidOperationException($"ملاحظة النتيجة لا تتجاوز {MaxTextLength} حرف");
            if (result == HandoverResult.WithNotes && record.Notes.Count == 0)
                throw new InvalidOperationException("سجّل ملاحظة واحدة على الأقل قبل تسجيل نتيجة «بملاحظات»");
            if (result == HandoverResult.WithoutNotes)
            {
                if (record.Notes.Count > 0) throw new InvalidOperationException("في السجل ملاحظات: النتيجة «بملاحظات»");
                if (record.Kind == HandoverKind.Final && otherOpenNotes > 0)
                    throw new InvalidOperationException($"في المشروع {otherOpenNotes} ملاحظة لم تُغلق: أغلقها قبل الاستلام النهائي بلا ملاحظات");
            }
        }

        /// <summary>إلغاء الطلب (بديل الحذف): لم ينتهِ بعد</summary>
        public static void EnsureCanCancel(Viewer viewer, HandoverRecord record)
        {
            Require(viewer, AppPermissions.ManageHandover, "إلغاء طلب الاستلام");
            ProjectRules.EnsureView(viewer, record.Project);
            ProjectRules.EnsureOpen(record.Project);
            if (record.Status is HandoverStatus.Concluded) throw new InvalidOperationException("انتهى الاستلام: لا يُلغى");
            if (record.Status is HandoverStatus.Cancelled) throw new InvalidOperationException("الطلب ملغى مسبقاً");
        }

        // ───────── ملاحظات اللجنة ─────────

        /// <summary>ملاحظة مقترحة: وصف مطلوب، ومسؤول موظف أو اسم (واحد منهما) أو لا أحد بعد، وموعد معقول</summary>
        public static string? ValidateNote(string? description, int? responsibleUserId, string? responsibleName, DateTime? dueDate, DateTime today)
        {
            if (string.IsNullOrWhiteSpace(description)) return "وصف الملاحظة مطلوب";
            if (description.Trim().Length > MaxTextLength) return $"وصف الملاحظة لا يتجاوز {MaxTextLength} حرف";
            if (responsibleUserId != null && !string.IsNullOrWhiteSpace(responsibleName)) return "المسؤول موظف في النظام أو جهة بالاسم (واحد منهما)";
            if ((responsibleName ?? string.Empty).Trim().Length > 150) return "اسم الجهة المسؤولة لا يتجاوز 150 حرفاً";
            if (dueDate is DateTime due && due.Date < today.Date.AddYears(-1)) return "موعد المعالجة بعيد في الماضي";
            return null;
        }

        /// <summary>المسؤول الموظف نشط ومن فرع المشروع</summary>
        public static string? ValidateResponsible(Project project, User? user)
        {
            if (user == null) return null;
            if (!user.IsActive) return "حساب هذا الموظف معطّل";
            if (!OrganizationRole.InBranch(user, project.BranchId)) return "المسؤول من موظفي فرع المشروع";
            return null;
        }

        /// <summary>
        /// إضافة ملاحظة: يسجّلها المخوَّل أثناء الكشف (لجنة مشكَّلة) أو بعد نتيجة «بملاحظات»؛ لا بعد «بلا ملاحظات» ولا قبل تشكيل اللجنة.
        /// </summary>
        public static void EnsureCanAddNote(Viewer viewer, HandoverRecord record)
        {
            Require(viewer, AppPermissions.FollowHandoverNotes, "تسجيل ملاحظات اللجنة");
            ProjectRules.EnsureView(viewer, record.Project);
            EnsureLive(record);
            if (record.Status == HandoverStatus.Requested) throw new InvalidOperationException("شكّل لجنة الاستلام قبل تسجيل ملاحظاتها");
            if (record.Status == HandoverStatus.Concluded && record.Result != HandoverResult.WithNotes)
                throw new InvalidOperationException("انتهى الاستلام بلا ملاحظات");
            if (record.Notes.Count >= MaxNotesPerRecord) throw new InvalidOperationException($"بلغ السجل الحد الأقصى للملاحظات ({MaxNotesPerRecord})");
        }

        /// <summary>
        /// تعديل ملاحظة لم تُعالج بعد (الوصف والمسؤول والموعد)، وحذفها ما دام السجل لم يُنهَ بنتيجة تستند إليها.
        /// </summary>
        public static void EnsureCanEditNote(Viewer viewer, HandoverRecord record, HandoverNote note)
        {
            Require(viewer, AppPermissions.FollowHandoverNotes, "تعديل ملاحظات اللجنة");
            ProjectRules.EnsureView(viewer, record.Project);
            EnsureLive(record);
            if (note.Status != HandoverNoteStatus.Open) throw new InvalidOperationException("عولجت هذه الملاحظة: لا تُعدَّل");
        }

        public static void EnsureCanRemoveNote(Viewer viewer, HandoverRecord record, HandoverNote note)
        {
            EnsureCanEditNote(viewer, record, note);
            if (record.Status == HandoverStatus.Concluded) throw new InvalidOperationException("انتهى الاستلام: لا تُحذف ملاحظاته");
        }

        /// <summary>تسجيل المعالجة: ملاحظة مفتوحة في سجل لجنته مشكَّلة أو منتهٍ بملاحظات، بنص المعالجة</summary>
        public static void EnsureCanMarkFixed(Viewer viewer, HandoverRecord record, HandoverNote note, string? fixNote)
        {
            Require(viewer, AppPermissions.FollowHandoverNotes, "تسجيل معالجة الملاحظة");
            ProjectRules.EnsureView(viewer, record.Project);
            EnsureLive(record);
            if (record.Status == HandoverStatus.Requested) throw new InvalidOperationException("لا معالجة قبل تشكيل اللجنة");
            if (note.Status != HandoverNoteStatus.Open) throw new InvalidOperationException("الملاحظة ليست مفتوحة");
            if (string.IsNullOrWhiteSpace(fixNote)) throw new InvalidOperationException("اكتب ما عولج");
            if (fixNote.Length > 1000) throw new InvalidOperationException("وصف المعالجة لا يتجاوز 1000 حرف");
        }

        /// <summary>
        /// التحقق من المعالجة وإغلاق الملاحظة: يتحقق غير من عالجها (حتى مدير النظام).
        /// </summary>
        public static void EnsureCanVerify(Viewer viewer, HandoverRecord record, HandoverNote note)
        {
            Require(viewer, AppPermissions.FollowHandoverNotes, "التحقق من معالجة الملاحظة");
            ProjectRules.EnsureView(viewer, record.Project);
            EnsureLive(record);
            if (note.Status != HandoverNoteStatus.Fixed) throw new InvalidOperationException("الملاحظة لم تُعالَج بعد");
            if (note.FixedByUserId == viewer.Id) throw new UnauthorizedAccessException("لا تتحقق من معالجة سجّلتها أنت: يتحقق منها غيرك");
        }

        /// <summary>رفض المعالجة عند التحقق: تعود الملاحظة مفتوحة بسبب مكتوب</summary>
        public static void EnsureCanRejectFix(Viewer viewer, HandoverRecord record, HandoverNote note, string? reason)
        {
            EnsureCanVerify(viewer, record, note);
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("اكتب سبب رفض المعالجة");
            if (reason.Length > 1000) throw new InvalidOperationException("السبب لا يتجاوز 1000 حرف");
        }

        // ───────── المتابعة ─────────

        public static int OpenNotes(IEnumerable<HandoverNote> notes) => notes.Count(n => n.Status != HandoverNoteStatus.Verified);

        public static bool IsNoteOverdue(HandoverNote note, DateTime today) =>
            note.Status != HandoverNoteStatus.Verified && note.DueDate is DateTime due && due.Date < today.Date;

        /// <summary>
        /// ما لم يكتمل قبل إغلاق المشروع (تنبيه لا منع): لا استلام نهائي بلا ملاحظات، ملاحظات مفتوحة، طلب لم ينتهِ، مبالغ محتجزة لم يُفرج عنها،
        /// وعقد ما زال سارياً. الإغلاق قرار على مرحلة المشروع يتخذه مخوَّل.
        /// </summary>
        public static List<string> ClosureWarnings(IReadOnlyCollection<HandoverRecord> records, IReadOnlyCollection<Contract> contracts)
        {
            var warnings = new List<string>();
            if (!records.Any(IsFinalWithoutNotes)) warnings.Add("لم يُسجَّل استلام نهائي بلا ملاحظات");
            var open = records.SelectMany(r => r.Notes).Count(n => n.Status != HandoverNoteStatus.Verified);
            if (open > 0) warnings.Add($"{open} ملاحظة استلام لم تُغلق");
            if (records.Any(r => r.Status is HandoverStatus.Requested or HandoverStatus.CommitteeFormed)) warnings.Add("طلب استلام لم ينتهِ");
            var withheld = contracts.Where(c => c.Status != ContractStatus.Cancelled).Sum(Contracts.ContractRules.StillWithheld);
            if (withheld > 0) warnings.Add("مبالغ محتجزة لم يُفرج عنها");
            if (contracts.Any(c => c.Status == ContractStatus.Active)) warnings.Add("عقد ما زال سارياً");
            return warnings;
        }
    }
}
