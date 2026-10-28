using System.Linq.Expressions;
using Application.Common;
using Domain.Entities;

namespace Application.Features.Diwan
{
    /// <summary>
    /// الديوان: المعاملات الواردة والصادرة (2026-10-28، المرحلة 2 من تحليل الجنوبية) — مصدر واحد لحدود الرؤية والقواعد الصرفة:
    /// - الحدّ ثابت في الكود لكل صلاحية: «معاملاتي» (سجّلتها أو أُحيلت إليّ أو مرّت بي) ثم قسمي ثم فرعي، والمؤسسة كلها لنظرة المؤسسة.
    /// - الإحالة إلى وحدة لا تفتح المعاملة لكل موظفيها (القاعدة 8): يعالجها حاملو HandleCorrespondence في الوحدة الحالية، وتبقى
    ///   مرئية لمن سجّلها ولمن مرّت به (من أحال ومن أُحيل إليه بالاسم).
    /// - الوارد يتتبّع الحالة (ذات المرحلة الثابتة)، والصادر يسير بمسار ثابت: مسودة ← معتمد بتوقيع (غير مُعدّها) ← صادر برقمه.
    /// - النظام ينبّه ولا يقرر: تجاوز الموعد «مرشّح تأخر» فقط، ولا تُغلق معاملة ولا تُعاد حالتها بمرور الوقت.
    /// </summary>
    public static class DiwanRules
    {
        public const int MaxAttachments = 20;
        public const int MaxTypeDeadlineDays = 3650;

        // ───────── الأسماء ─────────

        /// <summary>الرقم الداخلي IN-{سنة}-{تسلسل:D5} أو OUT-…؛ null للمسودة (لا تسلسل لها حتى الصدور)</summary>
        public static string? Number(CorrespondenceDirection direction, int year, int? sequence) =>
            sequence is int s ? $"{(direction == CorrespondenceDirection.Incoming ? "IN" : "OUT")}-{year}-{s:D5}" : null;

        public static string DirectionAr(CorrespondenceDirection direction) => direction == CorrespondenceDirection.Incoming ? "وارد" : "صادر";

        public static string StageAr(CorrespondenceStage stage) => stage switch
        {
            CorrespondenceStage.Registered => "مسجّلة",
            CorrespondenceStage.InProgress => "قيد المعالجة",
            CorrespondenceStage.Answered => "تمّت الإجابة",
            CorrespondenceStage.Closed => "مغلقة",
            _ => stage.ToString()
        };

        public static string OutgoingStateAr(OutgoingState state) => state switch
        {
            OutgoingState.Draft => "مسودة",
            OutgoingState.Approved => "معتمد بانتظار الصدور",
            OutgoingState.Issued => "صادر",
            _ => state.ToString()
        };

        /// <summary>تسمية حالة الصادر: المسودة المرفوعة تظهر «بانتظار الاعتماد» (الحالة الفعلية تبقى مسودة)</summary>
        public static string OutgoingStateAr(Correspondence c) =>
            IsSubmittedDraft(c) ? "بانتظار الاعتماد" : OutgoingStateAr(c.OutgoingState!.Value);

        /// <summary>مسودة رُفعت للاعتماد ولم يُبتّ فيها بعد</summary>
        public static bool IsSubmittedDraft(Correspondence c) => IsOutgoingDraft(c) && c.SubmittedAt != null;

        public static bool IsValidStage(int value) => Enum.IsDefined(typeof(CorrespondenceStage), value);

        // ───────── الحالة والمواعيد ─────────

        public static bool IsIncoming(Correspondence c) => c.Direction == CorrespondenceDirection.Incoming;

        /// <summary>مغلقة: حالة الوارد ذات مرحلة «مغلقة»، أو الصادر بعد صدوره (أرشيف)</summary>
        public static bool IsClosed(Correspondence c) =>
            c.Direction == CorrespondenceDirection.Incoming
                ? c.Status?.Stage == CorrespondenceStage.Closed
                : c.OutgoingState == OutgoingState.Issued;

        /// <summary>موعد المعالجة = تاريخ التسجيل (يوماً كاملاً) + مهلة النوع؛ null بلا مهلة</summary>
        public static DateTime? DueDate(DateTime registeredAt, int? deadlineDays) =>
            deadlineDays is int days and > 0 ? registeredAt.Date.AddDays(days) : null;

        /// <summary>
        /// مرشّح تأخر: وارد مفتوح (مسجّلة أو قيد المعالجة) تجاوز موعده. «تمّت الإجابة» و«مغلقة» لا تُحتسبان.
        /// مجرد تنبيه: لا تتغير حالتها ولا يُعدَّل موعدها.
        /// </summary>
        public static bool IsOverdueCandidate(Correspondence c, DateTime today) =>
            c.Direction == CorrespondenceDirection.Incoming
            && c.DueDate is DateTime due && due.Date < today.Date
            && c.Status?.Stage is CorrespondenceStage.Registered or CorrespondenceStage.InProgress;

        /// <summary>يحين موعدها اليوم أو غداً (تنبيه «قرب الموعد»)</summary>
        public static bool IsDueSoon(Correspondence c, DateTime today) =>
            c.Direction == CorrespondenceDirection.Incoming
            && c.DueDate is DateTime due && due.Date >= today.Date && due.Date <= today.Date.AddDays(1)
            && c.Status?.Stage is CorrespondenceStage.Registered or CorrespondenceStage.InProgress;

        public enum ReminderKind { DueSoon = 1, Overdue = 2 }

        /// <summary>
        /// التذكير المستحق الآن للوارد (مرة لكل موعد لكل نوع): «تجاوزت» أولاً إن كانت مرشّحة للتأخر ولم تُنبَّه، وإلا «قرب الموعد»
        /// إن حان اليوم أو غداً ولم تُنبَّه. صفة الحالة وحدها (مسجّلة/قيد المعالجة) تُدخلها، فالمجابة والمغلقة لا تذكير لها.
        /// </summary>
        public static ReminderKind? Reminder(Correspondence c, DateTime today)
        {
            if (IsOverdueCandidate(c, today)) return c.OverdueNotifiedAt == null ? ReminderKind.Overdue : null;
            if (IsDueSoon(c, today)) return c.DueSoonNotifiedAt == null ? ReminderKind.DueSoon : null;
            return null;
        }

        // ───────── التحقق من المدخلات ─────────

        /// <summary>سبب رفض بيانات التسجيل بالعربية، أو null</summary>
        public static string? ValidateRegistration(string? party, string? subject, string? summary, string? externalNumber)
        {
            if (string.IsNullOrWhiteSpace(subject)) return "موضوع المعاملة مطلوب";
            if (subject.Trim().Length > 300) return "الموضوع لا يتجاوز 300 حرف";
            if (string.IsNullOrWhiteSpace(party)) return "الجهة مطلوبة (المرسِلة للوارد، والمرسَل إليها للصادر)";
            if (party.Trim().Length > 200) return "اسم الجهة لا يتجاوز 200 حرف";
            if ((summary ?? string.Empty).Trim().Length > 4000) return "الملخص لا يتجاوز 4000 حرف";
            if ((externalNumber ?? string.Empty).Trim().Length > 50) return "رقم الكتاب لا يتجاوز 50 حرفاً";
            return null;
        }

        public static string? ValidateType(string? name, int? deadlineDays)
        {
            if (string.IsNullOrWhiteSpace(name)) return "اسم النوع مطلوب";
            if (name.Trim().Length > 100) return "اسم النوع لا يتجاوز 100 حرف";
            if (deadlineDays is < 1 or > MaxTypeDeadlineDays) return $"مهلة النوع من يوم إلى {MaxTypeDeadlineDays} يوم (أو بلا مهلة)";
            return null;
        }

        public static string? ValidateStatus(string? name, int stage, string? color)
        {
            if (string.IsNullOrWhiteSpace(name)) return "اسم الحالة مطلوب";
            if (name.Trim().Length > 100) return "اسم الحالة لا يتجاوز 100 حرف";
            if (!IsValidStage(stage)) return "مرحلة الحالة غير صالحة";
            if (!System.Text.RegularExpressions.Regex.IsMatch(color ?? string.Empty, "^#[0-9A-Fa-f]{6}$")) return "لون الحالة بصيغة #RRGGBB";
            return null;
        }

        // ───────── الحدود ─────────

        /// <summary>
        /// شرط رؤية المعاملة، قابل للترجمة إلى SQL (يعتمد على الإحالات): نظرة المؤسسة تراها كلها، وإلا اجتماع:
        /// فرعي (ViewBranchCorrespondence) | قسمي (سجّلها قسمي أو يحملها أو مرّت به) | معاملاتي (سجّلتها أو أُحيلت إليّ أو مرّت بي)
        /// | حامل المعالجة في الوحدة الحالية | المعتمِد لمسودات صادر فرعه.
        /// من لا يملك أيّاً من هذه لا يرى شيئاً، ولا يطابق مستخدم بلا وحدة أي وحدة.
        /// </summary>
        public static Expression<Func<Correspondence, bool>> Visible(Viewer viewer)
        {
            if (viewer.IsOrganizationWide) return c => true;

            var user = viewer.User;
            var userId = viewer.Id;
            int? branchId = viewer.Has(AppPermissions.ViewBranchCorrespondence) ? user.BranchId : null;
            int? approvesBranchId = viewer.Has(AppPermissions.ApproveOutgoingCorrespondence) ? user.BranchId : null;
            int? departmentId = viewer.Has(AppPermissions.ViewDepartmentCorrespondence) ? user.DepartmentId : null;
            var mine = viewer.Has(AppPermissions.ViewCorrespondence) || viewer.Has(AppPermissions.RegisterCorrespondence) || viewer.Has(AppPermissions.HandleCorrespondence);
            var handles = viewer.Has(AppPermissions.HandleCorrespondence);
            int? handleDepartmentId = handles && user.OfficeId == null ? user.DepartmentId : null;   // يتبع القسم مباشرة
            int? handleOfficeId = handles ? user.OfficeId : null;

            return c =>
                (branchId != null && c.BranchId == branchId)
                || (approvesBranchId != null && c.BranchId == approvesBranchId && c.Direction == CorrespondenceDirection.Outgoing)
                || (departmentId != null && (c.DepartmentId == departmentId || c.CurrentDepartmentId == departmentId
                        || c.Referrals.Any(r => r.ToDepartmentId == departmentId || r.FromDepartmentId == departmentId)))
                || (mine && (c.RegisteredByUserId == userId || c.CurrentUserId == userId
                        || c.Referrals.Any(r => r.FromUserId == userId || r.ToUserId == userId)))
                || (handleDepartmentId != null && c.CurrentDepartmentId == handleDepartmentId && c.CurrentOfficeId == null)
                || (handleOfficeId != null && c.CurrentOfficeId == handleOfficeId);
        }

        /// <summary>يرى معاملة محمَّلة بإحالاتها (للتحقق في الذاكرة بنفس شرط <see cref="Visible"/>)</summary>
        public static bool CanView(Viewer viewer, Correspondence c) => Visible(viewer).Compile()(c);

        public static void EnsureView(Viewer viewer, Correspondence c)
        {
            if (!CanView(viewer, c)) throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على هذه المعاملة");
        }

        /// <summary>
        /// هل تحمل وحدة المستخدم المعاملة الآن وهو يملك HandleCorrespondence؟ الوحدة الحالية قسم (يعالجها من يتبع القسم مباشرة)
        /// أو مكتب (يعالجها موظفو المكتب)، ومن حُدِّد بالاسم يعالجها أيضاً. مدير النظام دائماً.
        /// </summary>
        public static bool CanHandle(Viewer viewer, Correspondence c)
        {
            if (viewer.IsSuperAdmin) return true;
            if (!viewer.Has(AppPermissions.HandleCorrespondence)) return false;
            if (c.CurrentUserId == viewer.Id) return true;
            return c.CurrentOfficeId != null
                ? OrganizationRole.IsInOffice(viewer.User, c.CurrentOfficeId)
                : OrganizationRole.IsInDepartmentItself(viewer.User, c.CurrentDepartmentId);
        }

        /// <summary>
        /// «صندوقي الوارد»: الوارد المفتوح الذي تحمله وحدتي الآن (أو سُمّيتُ به) وأنا أملك المعالجة. شرط قابل للترجمة إلى SQL
        /// ومطابق لـ<see cref="CanHandle"/>. مدير النظام: كل وارد مفتوح.
        /// </summary>
        public static Expression<Func<Correspondence, bool>> Inbox(Viewer viewer)
        {
            if (viewer.IsSuperAdmin) return c => c.Direction == CorrespondenceDirection.Incoming && c.Status!.Stage != CorrespondenceStage.Closed;
            if (!viewer.Has(AppPermissions.HandleCorrespondence)) return c => false;

            var userId = viewer.Id;
            int? officeId = viewer.User.OfficeId;
            int? departmentId = officeId == null ? viewer.User.DepartmentId : null;
            return c => c.Direction == CorrespondenceDirection.Incoming && c.Status!.Stage != CorrespondenceStage.Closed
                && (c.CurrentUserId == userId
                    || (officeId != null && c.CurrentOfficeId == officeId)
                    || (departmentId != null && c.CurrentOfficeId == null && c.CurrentDepartmentId == departmentId));
        }

        // ───────── الوارد: التسجيل والإحالة والحالة ─────────

        /// <summary>تسجيل معاملة في فرع المستخدم (مدير النظام ونظرة المؤسسة يسجّلون لأي فرع يختارونه)</summary>
        public static void EnsureCanRegister(Viewer viewer, int branchId)
        {
            if (!viewer.Has(AppPermissions.RegisterCorrespondence) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية تسجيل المعاملات");
            if (!viewer.IsOrganizationWide && !OrganizationRole.InBranch(viewer.User, branchId))
                throw new UnauthorizedAccessException("لا تملك صلاحية التسجيل في فرع آخر");
        }

        public static void EnsureOpenIncoming(Correspondence c)
        {
            if (!IsIncoming(c)) throw new InvalidOperationException("هذه معاملة صادرة: لا تُحال ولا تتغير حالتها");
            if (IsClosed(c)) throw new InvalidOperationException("المعاملة مغلقة: أعد فتحها أولاً");
        }

        /// <summary>إحالة المعاملة التي تحملها وحدتي إلى وحدة في الفرع نفسه (الجهة المحال إليها مُتحقَّق من انتمائها للفرع في الـ handler)</summary>
        public static void EnsureCanRefer(Viewer viewer, Correspondence c)
        {
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ReferCorrespondence))
                throw new UnauthorizedAccessException("لا تملك صلاحية إحالة المعاملات");
            EnsureOpenIncoming(c);
            if (!CanHandle(viewer, c))
                throw new UnauthorizedAccessException("لا تحمل وحدتك هذه المعاملة الآن، فلا تحيلها");
        }

        /// <summary>
        /// الاستلام = تأكيد أن الجهة المحال إليها رأت المعاملة: يُسجَّل مرة واحدة على آخر إحالة، لمن يحملها الآن.
        /// يعيد الإحالة المطلوبة تأكيدها أو null إن لم تكن هناك إحالة بانتظار الاستلام.
        /// </summary>
        public static CorrespondenceReferral? PendingAcknowledgement(Correspondence c)
        {
            var last = c.Referrals.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).FirstOrDefault();
            return last is { AcknowledgedAt: null } ? last : null;
        }

        /// <summary>تغيير حالة الوارد إلى حالة من حالات فرعه (فعّالة)؛ الإغلاق وإعادة الفتح بـCloseCorrespondence</summary>
        public static void EnsureCanChangeStatus(Viewer viewer, Correspondence c, CorrespondenceStatus target)
        {
            if (!IsIncoming(c)) throw new InvalidOperationException("حالة المعاملة الصادرة تسير بمسارها الثابت (مسودة ← اعتماد ← صدور)");
            if (!CanHandle(viewer, c)) throw new UnauthorizedAccessException("لا تحمل وحدتك هذه المعاملة الآن");
            if (target.BranchId != c.BranchId) throw new InvalidOperationException("الحالة من فرع آخر");
            if (!target.IsActive) throw new InvalidOperationException("هذه الحالة معطّلة");

            var closing = target.Stage == CorrespondenceStage.Closed;
            var reopening = IsClosed(c) && !closing;
            if ((closing || reopening) && !viewer.IsSuperAdmin && !viewer.Has(AppPermissions.CloseCorrespondence))
                throw new UnauthorizedAccessException("إغلاق المعاملة وإعادة فتحها بصلاحية الإغلاق");
        }

        // ───────── الصادر: مسودة ← اعتماد ← صدور ─────────

        public static bool IsOutgoingDraft(Correspondence c) => c.Direction == CorrespondenceDirection.Outgoing && c.OutgoingState == OutgoingState.Draft;

        /// <summary>تعديل المسودة وإرفاقها: لمن أعدّها (أو من يملك التسجيل في فرعها)، ما دامت مسودة</summary>
        public static void EnsureCanEditDraft(Viewer viewer, Correspondence c)
        {
            if (!IsOutgoingDraft(c)) throw new InvalidOperationException("لا يُعدَّل إلا الصادر في طور المسودة");
            if (viewer.IsSuperAdmin) return;
            if (!viewer.Has(AppPermissions.RegisterCorrespondence) || !OrganizationRole.InBranch(viewer.User, c.BranchId))
                throw new UnauthorizedAccessException("لا تملك صلاحية تعديل هذه المسودة");
        }

        /// <summary>
        /// اعتماد المسودة أو إعادتها: ApproveOutgoingCorrespondence في فرعها، وليس من أعدّها (فصل الإعداد عن الاعتماد حتى لمدير النظام).
        /// الاعتماد يحتاج مرفقاً واحداً على الأقل (نص الكتاب)، وإعادتها تحتاج سبباً.
        /// </summary>
        public static void EnsureCanDecideDraft(Viewer viewer, Correspondence c, int attachmentCount, bool approve, string? note)
        {
            if (!IsOutgoingDraft(c)) throw new InvalidOperationException("لا يُعتمد إلا الصادر في طور المسودة");
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ApproveOutgoingCorrespondence))
                throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد الصادر");
            if (!viewer.IsSuperAdmin && !OrganizationRole.InBranch(viewer.User, c.BranchId))
                throw new UnauthorizedAccessException("لا تملك صلاحية اعتماد صادر فرع آخر");
            if (c.RegisteredByUserId == viewer.Id)
                throw new UnauthorizedAccessException("لا تعتمد مسودة أعددتَها بنفسك");
            if (approve && attachmentCount < 1)
                throw new InvalidOperationException("أرفق نص الكتاب (ملفاً واحداً على الأقل) قبل اعتماد الصادر");
            if (!approve && string.IsNullOrWhiteSpace(note))
                throw new InvalidOperationException("سبب إعادة المسودة مطلوب");
        }

        /// <summary>إصدار المعتمَد: يعطيه رقمه ويقفله. لمن يملك التسجيل في فرعه (الديوان)</summary>
        public static void EnsureCanIssue(Viewer viewer, Correspondence c)
        {
            if (c.Direction != CorrespondenceDirection.Outgoing || c.OutgoingState != OutgoingState.Approved)
                throw new InvalidOperationException("لا يُصدَر إلا الصادر المعتمد");
            if (viewer.IsSuperAdmin) return;
            if (!viewer.Has(AppPermissions.RegisterCorrespondence) || !OrganizationRole.InBranch(viewer.User, c.BranchId))
                throw new UnauthorizedAccessException("لا تملك صلاحية إصدار الكتب الصادرة");
        }

        // ───────── المرفقات ─────────

        /// <summary>إرفاق ملف: لمن يرى المعاملة ويعالجها (الوارد) أو يحرّر مسودتها (الصادر)؛ لا إرفاق على المغلق ولا على الصادر بعد الصدور</summary>
        public static void EnsureCanAttach(Viewer viewer, Correspondence c)
        {
            if (IsClosed(c)) throw new InvalidOperationException("المعاملة مغلقة: لا إرفاق عليها");
            if (c.Direction == CorrespondenceDirection.Outgoing)
            {
                if (c.OutgoingState == OutgoingState.Approved) throw new InvalidOperationException("الصادر المعتمد لا يُرفَق عليه");
                EnsureCanEditDraft(viewer, c);
                return;
            }
            if (!CanHandle(viewer, c) && !(viewer.Has(AppPermissions.RegisterCorrespondence) && c.RegisteredByUserId == viewer.Id))
                throw new UnauthorizedAccessException("لا تحمل وحدتك هذه المعاملة الآن");
            if (c.Attachments.Count >= MaxAttachments) throw new InvalidOperationException($"الحد الأقصى {MaxAttachments} ملفاً لكل معاملة");
        }
    }
}
