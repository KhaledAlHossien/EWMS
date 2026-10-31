using Application.Common;
using Application.Features.Projects;
using Domain.Entities;

namespace Application.Features.Contracts
{
    /// <summary>
    /// العقود والمقاولون (المرحلة 4، 2026-10-31) — مصدر واحد للقواعد الصرفة (بلا قاعدة بيانات):
    /// - حدّ كل صلاحية عقدية هو حدّ رؤية المشروع (<see cref="ProjectRules.Visible"/>)؛ والمقاولون ومعايير التقييم بيانات الفرع.
    /// - «النظام ينبّه ولا يقرر»: المبالغ والمدد وتاريخا الضمان تُدخَل يدوياً كما اعتُمدت (النسب والقواعد لم تُحدَّد بعد، فلا معادلة للاحتجاز ولا
    ///   للإفراج)، وحالة العقد لا تنتقل بمرور الوقت، وقرب انتهاء الضمان مرشّح تنبيه لا أكثر.
    /// - بعد سريان العقد تتجمّد طبيعته والمتعاقد معه وقيمته ومدته؛ يغيّرها ملحق معتمد فقط. العروض والملاحق والمحتجزات وقراراتها سجلات تُضاف ولا تُعدَّل ([ADR-0007]).
    /// - من أعدّ التقييم لا يراجعه حتى مدير النظام ([ADR-0008]).
    /// </summary>
    public static class ContractRules
    {
        public const int MaxNumberLength = 100;
        public const int MaxScopeLength = 4000;
        public const int MaxNotesLength = 2000;
        public const int MaxOffers = 50;
        public const int MaxAmendments = 100;
        public const int MaxDurationDays = 36_500;
        public const int MaxWarrantyMonths = 600;

        // ───────── الأسماء ─────────

        /// <summary>الرقم الداخلي CON-{سنة الإنشاء}-{id:D5} — يُحسب ولا يُخزَّن (غير الرقم الرسمي للعقد)</summary>
        public static string Number(int id, DateTime createdAt) => $"CON-{createdAt.Year}-{id:D5}";

        public static string MethodAr(ContractingMethod method) => method switch
        {
            ContractingMethod.Direct => "تنفيذ مباشر",
            ContractingMethod.Tender => "مناقصة",
            ContractingMethod.PriceQuotes => "عروض أسعار",
            _ => method.ToString()
        };

        public static string StatusAr(ContractStatus status) => status switch
        {
            ContractStatus.Contracting => "قيد التعاقد",
            ContractStatus.Active => "ساري",
            ContractStatus.Completed => "منجز",
            ContractStatus.Terminated => "مفسوخ",
            ContractStatus.Cancelled => "ملغى",
            _ => status.ToString()
        };

        public static string CommitteeKindAr(CommitteeKind kind) => kind == CommitteeKind.Handover ? "لجنة استلام" : "لجنة فنية ومالية";

        public static string RetentionDecisionAr(RetentionDecisionKind kind) => kind == RetentionDecisionKind.Release ? "إفراج" : "استمرار احتجاز";

        public static string EvaluationStatusAr(EvaluationStatus status) => status switch
        {
            EvaluationStatus.Draft => "مسودة",
            EvaluationStatus.Submitted => "مرسل إلى مكتب العقود",
            EvaluationStatus.Reviewed => "مراجَع ومحفوظ",
            _ => status.ToString()
        };

        public static bool IsValidMethod(int value) => Enum.IsDefined(typeof(ContractingMethod), value);
        public static bool IsValidStatus(int value) => Enum.IsDefined(typeof(ContractStatus), value);
        public static bool IsValidRetentionDecision(int value) => Enum.IsDefined(typeof(RetentionDecisionKind), value);

        // ───────── الصلاحية والرؤية ─────────

        private static void Require(Viewer viewer, string permission, string action)
        {
            if (!viewer.IsSuperAdmin && !viewer.Has(permission))
                throw new UnauthorizedAccessException($"لا تملك صلاحية {action}");
        }

        /// <summary>من لا يملك أي صلاحية عقود لا يرى شيئاً، ثم حدّ المشروع نفسه</summary>
        public static void EnsureCanView(Viewer viewer, Project project)
        {
            if (!AppPermissions.AnyOf["AnyContractView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض العقود");
            ProjectRules.EnsureView(viewer, project);
        }

        /// <summary>
        /// مقاولو الفرع ومعايير تقييمه: يراها ويديرها موظفو الفرع نفسه (ونظرة المؤسسة كل الفروع)؛ فرع آخر خارج الحدّ.
        /// </summary>
        public static void EnsureSameBranch(Viewer viewer, int branchId)
        {
            if (!viewer.IsOrganizationWide && !OrganizationRole.InBranch(viewer.User, branchId))
                throw new UnauthorizedAccessException("هذا السجل من فرع آخر");
        }

        // ───────── المقاولون ومعايير التقييم ─────────

        public static string? ValidateContractor(string? name, string? registrationNumber, string? contactName, string? phone, string? address, string? notes)
        {
            if (string.IsNullOrWhiteSpace(name)) return "اسم المقاول مطلوب";
            if (name.Trim().Length > 200) return "اسم المقاول لا يتجاوز 200 حرف";
            if ((registrationNumber ?? string.Empty).Length > 100) return "رقم السجل لا يتجاوز 100 حرف";
            if ((contactName ?? string.Empty).Length > 150) return "اسم المسؤول لا يتجاوز 150 حرفاً";
            if ((phone ?? string.Empty).Length > 50) return "رقم الهاتف لا يتجاوز 50 حرفاً";
            if ((address ?? string.Empty).Length > 300) return "العنوان لا يتجاوز 300 حرف";
            if ((notes ?? string.Empty).Length > MaxNotesLength) return $"الملاحظات لا تتجاوز {MaxNotesLength} حرف";
            return null;
        }

        /// <summary>المقاول المعطَّل لا يُختار لعقد جديد ولا عرض، وهو من فرع المشروع</summary>
        public static void EnsureContractorUsable(Contractor contractor, int branchId)
        {
            if (contractor.BranchId != branchId) throw new InvalidOperationException("المقاول من فرع آخر");
            if (!contractor.IsActive) throw new InvalidOperationException("المقاول معطَّل");
        }

        public static string? ValidateCriterion(string? name, string? description, decimal? weight, int sortOrder)
        {
            if (string.IsNullOrWhiteSpace(name)) return "اسم المعيار مطلوب";
            if (name.Trim().Length > 150) return "اسم المعيار لا يتجاوز 150 حرفاً";
            if ((description ?? string.Empty).Length > 500) return "وصف المعيار لا يتجاوز 500 حرف";
            if (weight is decimal w && (w <= 0 || w > 100)) return "وزن المعيار بين 0 و100 (اختياري)";
            if (sortOrder is < 0 or > 1000) return "الترتيب بين 0 و1000";
            return null;
        }

        // ───────── بيانات العقد ─────────

        /// <summary>رمز عملة من ثلاثة أحرف لاتينية كبيرة (SYP، USD، EUR…)</summary>
        public static bool IsValidCurrency(string? code) => code is { Length: 3 } && code.All(c => c is >= 'A' and <= 'Z');

        /// <summary>
        /// التحقق من حقول العقد (تُنفَّذ عند الفتح والتعديل). حقول المناقصة لا تُملأ إلا لطبيعة «مناقصة». المدة بالأيام موجبة، والقيمة غير سالبة،
        /// وتاريخ نهاية الضمان لا يسبق بدايته. لا حدّ أقصى للقيمة (تُدخَل كما اعتُمدت).
        /// </summary>
        public static string? ValidateContract(ContractingMethod method, string? number, decimal? value, string? currency, int? durationDays,
            DateTime? contractDate, DateTime? startDate, string? scope, string? notes,
            string? tenderNumber, DateTime? tenderSentAt, DateTime? tenderOpenedAt, DateTime? awardedAt, string? awardNote,
            int? warrantyMonths, DateTime? warrantyStart, DateTime? warrantyEnd, DateTime today)
        {
            if ((number ?? string.Empty).Trim().Length > MaxNumberLength) return $"رقم العقد لا يتجاوز {MaxNumberLength} حرف";
            if (value is decimal v && v < 0) return "القيمة التعاقدية لا تكون سالبة";
            if (!IsValidCurrency(currency)) return "رمز العملة ثلاثة أحرف لاتينية كبيرة (مثل SYP)";
            if (durationDays is int d && (d <= 0 || d > MaxDurationDays)) return "مدة التنفيذ بالأيام بين 1 و" + MaxDurationDays;
            if (contractDate is DateTime cd && cd.Date > today.Date.AddYears(1)) return "تاريخ العقد بعيد في المستقبل";
            if (contractDate is DateTime cd2 && cd2.Date < today.Date.AddYears(-30)) return "تاريخ العقد بعيد في الماضي";
            if (startDate is DateTime sd && contractDate is DateTime cd3 && sd.Date < cd3.Date) return "تاريخ بدء التنفيذ لا يسبق تاريخ العقد";
            if ((scope ?? string.Empty).Length > MaxScopeLength) return $"نطاق الأعمال لا يتجاوز {MaxScopeLength} حرف";
            if ((notes ?? string.Empty).Length > MaxNotesLength) return $"الملاحظات لا تتجاوز {MaxNotesLength} حرف";

            var hasTender = !string.IsNullOrWhiteSpace(tenderNumber) || tenderSentAt != null || tenderOpenedAt != null || awardedAt != null || !string.IsNullOrWhiteSpace(awardNote);
            if (hasTender && method != ContractingMethod.Tender) return "بيانات المناقصة تُسجَّل عندما تكون طبيعة التعاقد مناقصة";
            if ((tenderNumber ?? string.Empty).Length > 100) return "رقم المناقصة لا يتجاوز 100 حرف";
            if ((awardNote ?? string.Empty).Length > 1000) return "ملاحظة الإرساء لا تتجاوز 1000 حرف";
            if (tenderSentAt is DateTime ts && tenderOpenedAt is DateTime to && to.Date < ts.Date) return "فض المناقصة لا يسبق إرسالها";
            if (tenderOpenedAt is DateTime to2 && awardedAt is DateTime aw && aw.Date < to2.Date) return "الإرساء لا يسبق فض المناقصة";

            return ValidateWarranty(warrantyMonths, warrantyStart, warrantyEnd);
        }

        public static string? ValidateWarranty(int? months, DateTime? start, DateTime? end)
        {
            if (months is int m && (m <= 0 || m > MaxWarrantyMonths)) return "مدة الضمان بالأشهر بين 1 و" + MaxWarrantyMonths;
            if (start != null && end != null && end.Value.Date < start.Value.Date) return "نهاية الضمان لا تسبق بدايته";
            return null;
        }

        /// <summary>اقتراح نهاية الضمان من البداية والمدة (يعدّلها مكتب العقود بحسب القواعد الرسمية؛ لا يُكتب تلقائياً)</summary>
        public static DateTime? SuggestedWarrantyEnd(DateTime? start, int? months) =>
            start is DateTime s && months is int m && m > 0 ? s.Date.AddMonths(m).AddDays(-1) : null;

        // ───────── المراحل والتحرير ─────────

        public static void EnsureCanCreate(Viewer viewer, Project project)
        {
            Require(viewer, AppPermissions.CreateContract, "فتح سجل عقد");
            ProjectRules.EnsureView(viewer, project);
            ProjectRules.EnsureOpen(project);
        }

        /// <summary>تعديل بيانات العقد: لمن يملك الصلاحية، والمشروع غير منتهٍ، والعقد لم يُنهَ (منجز أو مفسوخ أو ملغى)</summary>
        public static void EnsureCanEdit(Viewer viewer, Contract contract)
        {
            Require(viewer, AppPermissions.EditContract, "تعديل العقد");
            ProjectRules.EnsureView(viewer, contract.Project);
            ProjectRules.EnsureOpen(contract.Project);
            EnsureNotClosed(contract);
        }

        public static void EnsureNotClosed(Contract contract)
        {
            if (contract.Status is ContractStatus.Terminated or ContractStatus.Cancelled)
                throw new InvalidOperationException($"العقد «{StatusAr(contract.Status)}»: لا يُعدَّل");
        }

        /// <summary>
        /// بعد السريان تتجمّد الطبيعة والمتعاقد معه والقيمة والمدة (يغيّرها ملحق معتمد). قبله تُعدَّل بحرية.
        /// </summary>
        public static void EnsureCoreFieldsUnchanged(Contract contract, ContractingMethod method, int? contractorId, decimal? value, int? durationDays)
        {
            if (contract.Status == ContractStatus.Contracting) return;
            if (method != contract.Method) throw new InvalidOperationException("طبيعة التعاقد لا تتغير بعد سريان العقد");
            if (contractorId != contract.ContractorId) throw new InvalidOperationException("المتعاقد معه لا يتغير بعد سريان العقد");
            if (value != contract.Value) throw new InvalidOperationException("القيمة التعاقدية لا تتغير بعد سريان العقد: سجّل ملحقاً معتمداً");
            if (durationDays != contract.DurationDays) throw new InvalidOperationException("مدة التنفيذ لا تتغير بعد سريان العقد: سجّل ملحقاً معتمداً");
        }

        /// <summary>الانتقالات المسموحة (تُسجَّل بقرار يدوي): قيد التعاقد ← ساري أو ملغى، وساري ← منجز أو مفسوخ. غير ذلك نهائي.</summary>
        public static bool CanTransition(ContractStatus from, ContractStatus to) => (from, to) switch
        {
            (ContractStatus.Contracting, ContractStatus.Active) => true,
            (ContractStatus.Contracting, ContractStatus.Cancelled) => true,
            (ContractStatus.Active, ContractStatus.Completed) => true,
            (ContractStatus.Active, ContractStatus.Terminated) => true,
            _ => false
        };

        /// <summary>
        /// تغيير الحالة بقرار صاحب التعديل. السريان يشترط الرقم الرسمي والمتعاقد معه وتاريخ العقد والقيمة، وللمناقصة وعروض الأسعار عرضاً مُرسًّى عليه
        /// هو عرض المتعاقد معه. الإنجاز والفسخ والإلغاء يشترطان سبباً في الملاحظة عند الفسخ والإلغاء.
        /// </summary>
        public static void EnsureCanChangeStatus(Viewer viewer, Contract contract, ContractStatus to, string? reason)
        {
            Require(viewer, AppPermissions.EditContract, "تغيير حالة العقد");
            ProjectRules.EnsureView(viewer, contract.Project);
            ProjectRules.EnsureOpen(contract.Project);
            if (!CanTransition(contract.Status, to))
                throw new InvalidOperationException($"لا يُنقل عقد «{StatusAr(contract.Status)}» إلى «{StatusAr(to)}»");
            if (to is ContractStatus.Terminated or ContractStatus.Cancelled && string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("اكتب سبب الفسخ أو الإلغاء");
            if ((reason ?? string.Empty).Length > 1000) throw new InvalidOperationException("السبب لا يتجاوز 1000 حرف");
            if (to == ContractStatus.Active) EnsureReadyToActivate(contract);
        }

        public static void EnsureReadyToActivate(Contract contract)
        {
            if (string.IsNullOrWhiteSpace(contract.Number)) throw new InvalidOperationException("أدخل الرقم الرسمي للعقد قبل سريانه");
            if (contract.ContractorId == null) throw new InvalidOperationException("حدّد المتعاقد معه قبل سريان العقد");
            if (contract.ContractDate == null) throw new InvalidOperationException("أدخل تاريخ العقد قبل سريانه");
            if (contract.Value == null) throw new InvalidOperationException("أدخل القيمة التعاقدية قبل سريان العقد");
            if (contract.Method != ContractingMethod.Direct)
            {
                var awarded = contract.Offers.FirstOrDefault(o => o.IsAwarded)
                    ?? throw new InvalidOperationException("سجّل العرض الذي رسا عليه التعاقد قبل سريان العقد");
                if (awarded.ContractorId != contract.ContractorId) throw new InvalidOperationException("المتعاقد معه ليس صاحب العرض المُرسّى عليه");
            }
        }

        // ───────── العروض والإرساء ─────────

        /// <summary>عرض جديد: للمناقصة وعروض الأسعار فقط، قبل الإرساء، من مقاول نشط من فرع المشروع لم يقدّم عرضاً</summary>
        public static void EnsureCanAddOffer(Viewer viewer, Contract contract, Contractor contractor, decimal amount)
        {
            EnsureCanEdit(viewer, contract);
            if (contract.Method == ContractingMethod.Direct) throw new InvalidOperationException("العروض لا تُسجَّل في التنفيذ المباشر");
            if (contract.Status != ContractStatus.Contracting) throw new InvalidOperationException("لا تُضاف عروض بعد سريان العقد");
            if (contract.Offers.Any(o => o.IsAwarded)) throw new InvalidOperationException("رسا التعاقد على عرض: لا تُضاف عروض");
            if (contract.Offers.Count >= MaxOffers) throw new InvalidOperationException($"بلغ العقد الحد الأقصى للعروض ({MaxOffers})");
            if (contract.Offers.Any(o => o.ContractorId == contractor.Id)) throw new InvalidOperationException("لهذا المقاول عرض مسجَّل على العقد");
            EnsureContractorUsable(contractor, contract.BranchId);
            if (amount < 0) throw new InvalidOperationException("مبلغ العرض لا يكون سالباً");
        }

        /// <summary>الإرساء على عرض بالقرار الرسمي: عقد قيد التعاقد، لا عرض مُرسًّى عليه سابقاً، والمقاول ما زال نشطاً</summary>
        public static void EnsureCanAward(Viewer viewer, Contract contract, ContractOffer offer)
        {
            EnsureCanEdit(viewer, contract);
            if (contract.Method == ContractingMethod.Direct) throw new InvalidOperationException("لا إرساء في التنفيذ المباشر");
            if (contract.Status != ContractStatus.Contracting) throw new InvalidOperationException("الإرساء قبل سريان العقد");
            if (contract.Offers.Any(o => o.IsAwarded)) throw new InvalidOperationException("رسا التعاقد على عرض مسبقاً");
            EnsureContractorUsable(offer.Contractor, contract.BranchId);
        }

        // ───────── الملاحق ─────────

        public static int NextAmendmentNumber(Contract contract) => contract.Amendments.Count == 0 ? 1 : contract.Amendments.Max(a => a.Number) + 1;

        /// <summary>القيمة بعد الملاحق المعتمدة (جمع بسيط لما سُجّل؛ لا معادلة)</summary>
        public static decimal? EffectiveValue(Contract contract) =>
            contract.Value is decimal v ? v + contract.Amendments.Sum(a => a.ValueChange ?? 0m) : null;

        public static int? EffectiveDurationDays(Contract contract) =>
            contract.DurationDays is int d ? d + contract.Amendments.Sum(a => a.DurationChangeDays ?? 0) : null;

        /// <summary>ملحق على عقد ساري أو منجز، بعنوان وتاريخ اعتماد، ولا ينزل بالقيمة أو المدة إلى ما دون الصفر</summary>
        public static void EnsureCanAddAmendment(Viewer viewer, Contract contract, string? title, string? description, decimal? valueChange, int? durationChangeDays, DateTime approvedAt, DateTime today)
        {
            Require(viewer, AppPermissions.AddContractAmendment, "تسجيل ملحق على عقد");
            ProjectRules.EnsureView(viewer, contract.Project);
            ProjectRules.EnsureOpen(contract.Project);
            if (contract.Status is not (ContractStatus.Active or ContractStatus.Completed))
                throw new InvalidOperationException("الملاحق على العقود السارية فقط");
            if (contract.Amendments.Count >= MaxAmendments) throw new InvalidOperationException($"بلغ العقد الحد الأقصى للملاحق ({MaxAmendments})");
            if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("عنوان الملحق مطلوب");
            if (title.Trim().Length > 200) throw new InvalidOperationException("عنوان الملحق لا يتجاوز 200 حرف");
            if ((description ?? string.Empty).Length > 2000) throw new InvalidOperationException("وصف الملحق لا يتجاوز 2000 حرف");
            if (approvedAt.Date > today.Date) throw new InvalidOperationException("تاريخ اعتماد الملحق لا يكون في المستقبل");
            if (valueChange == null && durationChangeDays == null && string.IsNullOrWhiteSpace(description))
                throw new InvalidOperationException("اذكر أثر الملحق على القيمة أو المدة أو اشرحه");
            if (valueChange is decimal vc && EffectiveValue(contract) is decimal ev && ev + vc < 0)
                throw new InvalidOperationException("الملحق ينزل بقيمة العقد إلى ما دون الصفر");
            if (durationChangeDays is int dc && EffectiveDurationDays(contract) is int ed && ed + dc <= 0)
                throw new InvalidOperationException("الملحق ينزل بمدة التنفيذ إلى صفر أو أقل");
        }

        // ───────── اللجان (فنية ومالية لعقد، أو استلام) ─────────

        public const int MaxCommitteeMembers = 30;

        /// <summary>عضو مقترح في لجنة: موظف أو اسم خارجي (واحد منهما)، بصفته</summary>
        public sealed record MemberInput(int? UserId, string? ExternalName, string? Capacity, bool IsChair);

        /// <summary>
        /// الأعضاء: عضو واحد على الأقل، موظف أو اسم خارجي لكل واحد، صفة مطلوبة، رئيس واحد بالضبط، ولا موظف مكرر.
        /// </summary>
        public static string? ValidateMembers(IReadOnlyCollection<MemberInput> members)
        {
            if (members.Count == 0) return "أضف عضواً واحداً على الأقل";
            if (members.Count > MaxCommitteeMembers) return $"عدد الأعضاء لا يتجاوز {MaxCommitteeMembers}";
            foreach (var m in members)
            {
                var hasUser = m.UserId != null;
                var hasName = !string.IsNullOrWhiteSpace(m.ExternalName);
                if (hasUser == hasName) return "كل عضو موظف في النظام أو اسم من خارجه (واحد منهما)";
                if (hasName && m.ExternalName!.Trim().Length > 150) return "اسم العضو لا يتجاوز 150 حرفاً";
                if (string.IsNullOrWhiteSpace(m.Capacity)) return "صفة العضو مطلوبة";
                if (m.Capacity.Trim().Length > 100) return "صفة العضو لا تتجاوز 100 حرف";
            }
            if (members.Count(m => m.IsChair) != 1) return "حدّد رئيساً واحداً للجنة";
            var users = members.Where(m => m.UserId != null).Select(m => m.UserId).ToList();
            if (users.Count != users.Distinct().Count()) return "موظف مكرر في أعضاء اللجنة";
            return null;
        }

        public static string? ValidateCommittee(string? decisionNumber, string? recommendations, DateTime formedAt, DateTime today)
        {
            if ((decisionNumber ?? string.Empty).Length > 100) return "رقم قرار التشكيل لا يتجاوز 100 حرف";
            if ((recommendations ?? string.Empty).Length > 4000) return "التوصيات لا تتجاوز 4000 حرف";
            if (formedAt.Date > today.Date) return "تاريخ التشكيل لا يكون في المستقبل";
            if (formedAt.Date < today.Date.AddYears(-30)) return "تاريخ التشكيل بعيد في الماضي";
            return null;
        }

        /// <summary>تشكيل لجنة العقد الفنية والمالية وتعديل أعضائها: للمخوَّل، وفي عقد لم يُفسخ أو يُلغَ</summary>
        public static void EnsureCanManageCommittee(Viewer viewer, Contract contract)
        {
            Require(viewer, AppPermissions.ManageContractCommittees, "إدارة لجان العقد");
            ProjectRules.EnsureView(viewer, contract.Project);
            ProjectRules.EnsureOpen(contract.Project);
            EnsureNotClosed(contract);
        }

        // ───────── المبالغ المحتجزة وقرارها ─────────

        public enum RetentionState
        {
            Withheld = 1,    // محتجز: لم يصدر فيه قرار
            Continued = 2,   // استمر احتجازه بقرار
            Released = 3     // أُفرج عنه (نهائي)
        }

        /// <summary>حالة المبلغ من آخر قرار مسجَّل (بالتاريخ ثم الترتيب)</summary>
        public static RetentionState StateOf(RetentionEntry entry)
        {
            var last = entry.Decisions.OrderBy(d => d.DecidedAt).ThenBy(d => d.Id).LastOrDefault();
            return last == null ? RetentionState.Withheld : last.Decision == RetentionDecisionKind.Release ? RetentionState.Released : RetentionState.Continued;
        }

        public static string RetentionStateAr(RetentionState state) => state switch
        {
            RetentionState.Withheld => "محتجز",
            RetentionState.Continued => "استمر احتجازه",
            _ => "أُفرج عنه"
        };

        /// <summary>مجموع المحتجز غير المفرج عنه (جمع بسيط لما أُدخل)</summary>
        public static decimal StillWithheld(Contract contract) =>
            contract.RetentionEntries.Where(e => StateOf(e) != RetentionState.Released).Sum(e => e.Amount);

        public static void EnsureCanRecordRetention(Viewer viewer, Contract contract, decimal amount, DateTime withheldAt, string? note, DateTime today)
        {
            Require(viewer, AppPermissions.RecordContractRetention, "تسجيل المبالغ المحتجزة");
            ProjectRules.EnsureView(viewer, contract.Project);
            ProjectRules.EnsureOpen(contract.Project);
            if (contract.Status is not (ContractStatus.Active or ContractStatus.Completed))
                throw new InvalidOperationException("المبالغ المحتجزة تُسجَّل على عقد ساري أو منجز");
            if (amount <= 0) throw new InvalidOperationException("المبلغ المحتجز أكبر من صفر");
            if (withheldAt.Date > today.Date) throw new InvalidOperationException("تاريخ الاحتجاز لا يكون في المستقبل");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");
        }

        /// <summary>
        /// قرار الإفراج أو استمرار الاحتجاز بالاعتماد الرسمي: يلزم تاريخ القرار (لا قبل الاحتجاز ولا في المستقبل) وجهة الاعتماد،
        /// والإفراج نهائي فلا قرار بعده.
        /// </summary>
        public static void EnsureCanDecideRetention(Viewer viewer, Contract contract, RetentionEntry entry, RetentionDecisionKind decision, DateTime decidedAt, string? authority, string? note, DateTime today)
        {
            Require(viewer, AppPermissions.RecordContractRetention, "تسجيل قرار المبالغ المحتجزة");
            ProjectRules.EnsureView(viewer, contract.Project);
            ProjectRules.EnsureOpen(contract.Project);
            if (StateOf(entry) == RetentionState.Released) throw new InvalidOperationException("أُفرج عن هذا المبلغ: القرار نهائي");
            if (!IsValidRetentionDecision((int)decision)) throw new InvalidOperationException("القرار إفراج أو استمرار احتجاز");
            if (string.IsNullOrWhiteSpace(authority)) throw new InvalidOperationException("اكتب الجهة التي اعتمدت القرار");
            if (authority.Trim().Length > 200) throw new InvalidOperationException("جهة الاعتماد لا تتجاوز 200 حرف");
            if (decidedAt.Date > today.Date) throw new InvalidOperationException("تاريخ القرار لا يكون في المستقبل");
            if (decidedAt.Date < entry.WithheldAt.Date) throw new InvalidOperationException("تاريخ القرار لا يسبق تاريخ الاحتجاز");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");
        }

        // ───────── الضمان (تنبيه لا قرار) ─────────

        /// <summary>
        /// مرشّح لتنبيه قرب انتهاء الضمان: عقد ساري أو منجز تنتهي مدة ضمانه خلال <paramref name="withinDays"/> يوماً من اليوم (ولم تنتهِ بعد).
        /// قرب الانتهاء وحده لا يغيّر شيئاً.
        /// </summary>
        public static bool IsWarrantyEndingSoon(Contract contract, DateTime today, int withinDays) =>
            contract.Status is ContractStatus.Active or ContractStatus.Completed
            && contract.WarrantyEnd is DateTime end
            && end.Date >= today.Date && end.Date <= today.Date.AddDays(withinDays);

        /// <summary>تنبيه واحد لكل نهاية ضمان: لم يُنبَّه عنها، أو غُيّرت النهاية بعد التنبيه</summary>
        public static bool NeedsWarrantyNotice(Contract contract) =>
            contract.WarrantyEnd is DateTime end && contract.WarrantyNotifiedFor?.Date != end.Date;

        /// <summary>أيام باقية على نهاية الضمان (سالب بعد الانتهاء؛ null إن لم تُحدَّد)</summary>
        public static int? WarrantyDaysLeft(Contract contract, DateTime today) =>
            contract.WarrantyEnd is DateTime end ? (int)(end.Date - today.Date).TotalDays : null;

        // ───────── تقييم المقاول ─────────

        public const int MaxEvaluationNotes = 2000;

        /// <summary>درجة معيار واحد (اختيارية، 0–100، يدوية) مع وزنه ونسخة اسمه</summary>
        public sealed record ScoreInput(int CriterionId, decimal? Score, string? Note);

        /// <summary>
        /// الدرجات: المعايير من فرع العقد ونشطة وغير مكررة، والدرجة (إن وُجدت) بين 0 و100، والملاحظة حتى 500 حرف.
        /// لا يُحتسب مجموع ولا تصنيف (المعايير وطريقة الدرجة لم تُعتمد رسمياً).
        /// </summary>
        public static string? ValidateScores(IReadOnlyCollection<ScoreInput> scores, IReadOnlyDictionary<int, EvaluationCriterion> criteria, int branchId)
        {
            if (scores.Count != scores.Select(s => s.CriterionId).Distinct().Count()) return "معيار مكرر في التقييم";
            foreach (var s in scores)
            {
                if (!criteria.TryGetValue(s.CriterionId, out var c)) return "معيار غير موجود";
                if (c.BranchId != branchId) return "معيار من فرع آخر";
                if (!c.IsActive) return $"المعيار «{c.Name}» معطَّل";
                if (s.Score is decimal v && (v < 0 || v > 100)) return $"درجة «{c.Name}» بين 0 و100";
                if ((s.Note ?? string.Empty).Length > 500) return $"ملاحظة «{c.Name}» لا تتجاوز 500 حرف";
            }
            return null;
        }

        /// <summary>
        /// إعداد تقييم: لمن يملك الصلاحية، وهو مشارك في المشروع (إلا مدير النظام)، وعلى عقد له متعاقد معه ولم يُفسخ أو يُلغَ.
        /// </summary>
        public static void EnsureCanEvaluate(Viewer viewer, Contract contract)
        {
            Require(viewer, AppPermissions.EvaluateContractor, "تقييم المقاول");
            ProjectRules.EnsureView(viewer, contract.Project);
            ProjectRules.EnsureOpen(contract.Project);
            if (!viewer.IsSuperAdmin && contract.Project.Members.All(m => m.UserId != viewer.Id))
                throw new UnauthorizedAccessException("التقييم لمشاركي المشروع");
            if (contract.ContractorId == null) throw new InvalidOperationException("لا مقاول متعاقد معه على هذا العقد");
            if (contract.Status is not (ContractStatus.Active or ContractStatus.Completed))
                throw new InvalidOperationException("يُقيَّم المقاول على عقد ساري أو منجز");
        }

        /// <summary>تعديل المسودة وإرسالها: لمن أعدّها (أو مدير النظام)، والتقييم مسودة، وفيه درجة واحدة على الأقل عند الإرسال</summary>
        public static void EnsureCanEditEvaluation(Viewer viewer, ContractorEvaluation evaluation)
        {
            if (evaluation.Status != EvaluationStatus.Draft) throw new InvalidOperationException($"التقييم «{EvaluationStatusAr(evaluation.Status)}»: لا يُعدَّل");
            if (!viewer.IsSuperAdmin && evaluation.EvaluatedByUserId != viewer.Id) throw new UnauthorizedAccessException("هذا التقييم أعدّه غيرك");
        }

        public static void EnsureCanSubmitEvaluation(Viewer viewer, Contract contract, ContractorEvaluation evaluation)
        {
            EnsureCanEvaluate(viewer, contract);
            EnsureCanEditEvaluation(viewer, evaluation);
            if (!evaluation.Scores.Any(s => s.Score != null)) throw new InvalidOperationException("أدخل درجة معيار واحد على الأقل قبل الإرسال");
        }

        /// <summary>
        /// مراجعة تقييم مرسل وحفظه في ملف المقاول: لمن يملك صلاحية المراجعة، وليس من أعدّه (حتى مدير النظام). المراجَع لا يُعدَّل.
        /// </summary>
        public static void EnsureCanReviewEvaluation(Viewer viewer, Contract contract, ContractorEvaluation evaluation, string? note)
        {
            Require(viewer, AppPermissions.ReviewContractorEvaluation, "مراجعة تقييم المقاول");
            ProjectRules.EnsureView(viewer, contract.Project);
            ProjectRules.EnsureOpen(contract.Project);
            if (evaluation.Status != EvaluationStatus.Submitted) throw new InvalidOperationException("التقييم ليس بانتظار المراجعة");
            if (viewer.Id == evaluation.EvaluatedByUserId) throw new UnauthorizedAccessException("لا تراجع تقييماً أعددته: يراجعه غيرك");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("ملاحظة المراجعة لا تتجاوز 1000 حرف");
        }

        /// <summary>
        /// إعادة تقييم مرسل إلى مُعدّه لاستكماله (بسبب مكتوب): تُعاده المراجعة (غير المُعدّ)، فيعود مسودة.
        /// </summary>
        public static void EnsureCanReturnEvaluation(Viewer viewer, Contract contract, ContractorEvaluation evaluation, string? reason)
        {
            EnsureCanReviewEvaluation(viewer, contract, evaluation, reason);
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("اكتب سبب إعادة التقييم");
        }
    }
}
