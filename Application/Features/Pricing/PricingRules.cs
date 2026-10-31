using System.Linq.Expressions;
using Application.Common;
using Application.Features.Contracts;
using Application.Features.Projects;
using Domain.Entities;

namespace Application.Features.Pricing
{
    /// <summary>
    /// مكتب التسعير (المرحلة 5أ، 2026-10-31) — مصدر واحد للقواعد الصرفة (بلا قاعدة بيانات):
    /// - الحدّ فرع الطلب (ونظرة المؤسسة كل الفروع)؛ ومقدّم الطلب والمكلَّف يريان طلبيهما دائماً. وربط الطلب بمشروع يشترط رؤيته.
    /// - «النظام ينبّه ولا يقرر»: الأسعار والكميات أرقام تُدخَل يدوياً؛ <b>لا مقارنة بين وحدات أو عملات مختلفة</b> (لا جداول تحويل معتمدة)،
    ///   و<b>لا يُختار عرض آلياً</b> لأنه الأرخص: الاختيار بيد المكلَّف بسبب مكتوب، ولا يقع على بند لم تتحقق مطابقته الفنية أو انتهت صلاحيته.
    /// - من أوصى بالاختيار لا يعتمده حتى مدير النظام، ومن أعدّ محضر الاتفاق لا يعتمده كذلك ([ADR-0008]).
    /// - إعداد محضر الاتفاق لمدير المكتب وحده بصلاحية مستقلة (القيد وظيفي لا بالاسم، [ADR-0002]).
    /// - الاعتماد النهائي لا يُحذف أثره: الطلب يُلغى ولا يُحذف، والمحضر المعتمد لا يُعدَّل ([ADR-0007]).
    /// </summary>
    public static class PricingRules
    {
        public const int MaxItems = 100;
        public const int MaxOffers = 30;
        public const int MaxAgreementLines = 100;
        public const int MaxTitleLength = 200;

        /// <summary>قرار المعتمِد على التوصية: اعتماد، رفض، أو إعادة لجمع العروض</summary>
        public enum Decision { Approve = 1, Reject = 2, Return = 3 }

        // ───────── الأسماء ─────────

        /// <summary>الرقم الداخلي PRC-{سنة الإنشاء}-{id:D5} — يُحسب ولا يُخزَّن</summary>
        public static string Number(int id, DateTime createdAt) => $"PRC-{createdAt.Year}-{id:D5}";

        /// <summary>الرقم الداخلي AGR-{سنة الإنشاء}-{id:D5} — غير الرقم الرسمي للمحضر</summary>
        public static string AgreementNumber(int id, DateTime createdAt) => $"AGR-{createdAt.Year}-{id:D5}";

        public static string StatusAr(PricingRequestStatus status) => status switch
        {
            PricingRequestStatus.Registered => "مسجَّل بانتظار التكليف",
            PricingRequestStatus.Collecting => "قيد جمع العروض",
            PricingRequestStatus.Recommended => "بانتظار الاعتماد",
            PricingRequestStatus.Approved => "معتمد",
            PricingRequestStatus.Rejected => "مرفوض",
            PricingRequestStatus.ResultSent => "أُرسلت النتيجة",
            PricingRequestStatus.Cancelled => "ملغى",
            _ => status.ToString()
        };

        public static string ConformityAr(ConformityResult result) => result switch
        {
            ConformityResult.NotEvaluated => "لم تُقيَّم",
            ConformityResult.Conforming => "مطابق",
            ConformityResult.NonConforming => "غير مطابق",
            _ => result.ToString()
        };

        public static string AgreementStatusAr(AgreementStatus status) => status switch
        {
            AgreementStatus.Draft => "مسودة",
            AgreementStatus.Submitted => "مرسل للاعتماد",
            AgreementStatus.Approved => "معتمد",
            AgreementStatus.Returned => "معاد للتصحيح",
            AgreementStatus.Cancelled => "ملغى",
            _ => status.ToString()
        };

        public static bool IsValidConformity(int value) => Enum.IsDefined(typeof(ConformityResult), value);

        // ───────── الصلاحية والرؤية ─────────

        private static void Require(Viewer viewer, string permission, string action)
        {
            if (!viewer.IsSuperAdmin && !viewer.Has(permission))
                throw new UnauthorizedAccessException($"لا تملك صلاحية {action}");
        }

        /// <summary>فرع الطلب أو المحضر هو فرع المستخدم، ولنظرة المؤسسة أي فرع</summary>
        public static bool InBranch(Viewer viewer, int branchId) => viewer.IsOrganizationWide || OrganizationRole.InBranch(viewer.User, branchId);

        private static void EnsureInBranch(Viewer viewer, int branchId)
        {
            if (!InBranch(viewer, branchId)) throw new UnauthorizedAccessException("هذا السجل من فرع آخر");
        }

        /// <summary>شرط رؤية الطلبات، قابل للترجمة إلى SQL: صاحب «عرض الطلبات» يرى فرعه، ومقدّم الطلب والمكلَّف يريان طلبيهما</summary>
        public static Expression<Func<PricingRequest, bool>> Visible(Viewer viewer)
        {
            var id = viewer.Id;
            if (viewer.IsSuperAdmin || (viewer.IsOrganizationWide && viewer.Has(AppPermissions.ViewPricingRequests)))
                return r => true;
            int? branch = viewer.Has(AppPermissions.ViewPricingRequests) ? viewer.User.BranchId : null;
            return r => (branch != null && r.BranchId == branch) || r.RegisteredByUserId == id || r.AssigneeUserId == id;
        }

        public static bool CanView(Viewer viewer, PricingRequest request) => Visible(viewer).Compile()(request);

        /// <summary>من لا يملك أي صلاحية تسعير لا يرى شيئاً، ثم حدّ الطلب نفسه</summary>
        public static void EnsureCanView(Viewer viewer, PricingRequest request)
        {
            if (!AppPermissions.AnyOf["AnyPricingRequestView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض طلبات التسعير");
            if (!CanView(viewer, request)) throw new UnauthorizedAccessException("هذا الطلب خارج صلاحياتك");
        }

        /// <summary>من لا يملك أي صلاحية محاضر لا يرى شيئاً، ثم فرع المحضر</summary>
        public static void EnsureCanViewAgreement(Viewer viewer, PricingAgreement agreement)
        {
            if (!AppPermissions.AnyOf["AnyPricingAgreementView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض محاضر الاتفاق");
            EnsureInBranch(viewer, agreement.BranchId);
        }

        // ───────── الطلب وبنوده ─────────

        /// <summary>بند مقترح: وصف ومواصفات وكمية ووحدة</summary>
        public sealed record ItemInput(string? Description, string? Specifications, decimal Quantity, string? Unit);

        public static string? ValidateRequest(string? title, string? description, DateTime? dueDate, IReadOnlyCollection<ItemInput> items, DateTime today)
        {
            if (string.IsNullOrWhiteSpace(title)) return "عنوان الطلب مطلوب";
            if (title.Trim().Length > MaxTitleLength) return $"عنوان الطلب لا يتجاوز {MaxTitleLength} حرف";
            if ((description ?? string.Empty).Length > 4000) return "الوصف لا يتجاوز 4000 حرف";
            if (dueDate is DateTime due && due.Date < today.Date.AddYears(-1)) return "الموعد المطلوب بعيد في الماضي";
            return ValidateItems(items);
        }

        public static string? ValidateItems(IReadOnlyCollection<ItemInput> items)
        {
            if (items.Count == 0) return "أضف بنداً واحداً على الأقل";
            if (items.Count > MaxItems) return $"عدد البنود لا يتجاوز {MaxItems}";
            foreach (var i in items)
            {
                if (string.IsNullOrWhiteSpace(i.Description)) return "وصف البند مطلوب";
                if (i.Description.Trim().Length > 500) return "وصف البند لا يتجاوز 500 حرف";
                if ((i.Specifications ?? string.Empty).Length > 2000) return "مواصفات البند لا تتجاوز 2000 حرف";
                if (i.Quantity <= 0) return "كمية البند أكبر من صفر";
                if (i.Quantity > 1_000_000_000_000m) return "كمية البند كبيرة جداً";
                if (string.IsNullOrWhiteSpace(i.Unit)) return "وحدة البند مطلوبة";
                if (i.Unit.Trim().Length > 30) return "وحدة البند لا تتجاوز 30 حرفاً";
            }
            return null;
        }

        /// <summary>
        /// التسجيل: لمن يملك الصلاحية؛ وربطه بمشروع يشترط رؤيته وأن يكون مفتوحاً، وبعقد من المشروع نفسه (ساري أو منجز).
        /// </summary>
        public static void EnsureCanRegister(Viewer viewer, Project? project, Contract? contract)
        {
            Require(viewer, AppPermissions.RegisterPricingRequest, "تسجيل طلب تسعير");
            if (project != null)
            {
                ProjectRules.EnsureView(viewer, project);
                ProjectRules.EnsureOpen(project);
            }
            if (contract != null)
            {
                if (project == null || contract.ProjectId != project.Id) throw new InvalidOperationException("العقد من مشروع آخر: اختر مشروعه");
                if (contract.Status is ContractStatus.Cancelled or ContractStatus.Terminated) throw new InvalidOperationException("العقد ملغى أو مفسوخ");
            }
        }

        private static bool CanManage(Viewer viewer, PricingRequest request) => viewer.IsSuperAdmin || (viewer.Has(AppPermissions.AssignPricingRequest) && InBranch(viewer, request.BranchId));

        /// <summary>
        /// تعديل الطلب: لصاحب «التكليف» في فرعه، أو لمقدّمه ما دام «مسجَّلاً» لم يُكلَّف به أحد؛ والطلب مسجَّل أو قيد جمع العروض.
        /// </summary>
        public static void EnsureCanEdit(Viewer viewer, PricingRequest request)
        {
            var registrantWhileRegistered = request.RegisteredByUserId == viewer.Id && request.Status == PricingRequestStatus.Registered && viewer.Has(AppPermissions.RegisterPricingRequest);
            if (!CanManage(viewer, request) && !registrantWhileRegistered) throw new UnauthorizedAccessException("لا تملك صلاحية تعديل هذا الطلب");
            if (request.Status is not (PricingRequestStatus.Registered or PricingRequestStatus.Collecting))
                throw new InvalidOperationException($"الطلب «{StatusAr(request.Status)}»: لا يُعدَّل");
        }

        /// <summary>البنود تتغير قبل تسجيل أول عرض فقط (العروض تسعّر بنوداً بعينها)</summary>
        public static void EnsureItemsEditable(PricingRequest request)
        {
            if (request.Offers.Count > 0) throw new InvalidOperationException("سُجّلت عروض على الطلب: لا تتغير بنوده");
        }

        /// <summary>
        /// تكليف موظف المكتب بالمتابعة أو تغييره: لصاحب «التكليف» في فرع الطلب؛ والمكلَّف موظف نشط من الفرع ويملك صلاحية إعداد العروض
        /// (<paramref name="assigneeCanPrepare"/>).
        /// </summary>
        public static void EnsureCanAssign(Viewer viewer, PricingRequest request, User assignee, bool assigneeCanPrepare)
        {
            Require(viewer, AppPermissions.AssignPricingRequest, "تكليف موظف المكتب");
            EnsureInBranch(viewer, request.BranchId);
            if (request.Status is not (PricingRequestStatus.Registered or PricingRequestStatus.Collecting))
                throw new InvalidOperationException($"الطلب «{StatusAr(request.Status)}»: لا يُكلَّف به أحد");
            if (!assignee.IsActive) throw new InvalidOperationException("حساب هذا الموظف معطّل");
            if (!OrganizationRole.InBranch(assignee, request.BranchId)) throw new InvalidOperationException("المكلَّف من موظفي فرع الطلب");
            if (!assigneeCanPrepare) throw new InvalidOperationException("هذا الموظف لا يملك صلاحية إعداد العروض");
        }

        /// <summary>الإلغاء بديل الحذف: لصاحب «التكليف» أو لمقدّمه قبل التكليف؛ لا بعد التوصية ولا الاعتماد</summary>
        public static void EnsureCanCancel(Viewer viewer, PricingRequest request)
        {
            var registrantBeforeAssign = request.RegisteredByUserId == viewer.Id && request.Status == PricingRequestStatus.Registered && viewer.Has(AppPermissions.RegisterPricingRequest);
            if (!CanManage(viewer, request) && !registrantBeforeAssign) throw new UnauthorizedAccessException("لا تملك صلاحية إلغاء هذا الطلب");
            if (request.Status == PricingRequestStatus.Recommended) throw new InvalidOperationException("بانتظار الاعتماد: يعيده المعتمِد لجمع العروض أو يرفضه");
            if (request.Status is not (PricingRequestStatus.Registered or PricingRequestStatus.Collecting))
                throw new InvalidOperationException($"الطلب «{StatusAr(request.Status)}»: لا يُلغى");
        }

        // ───────── العروض وبنودها ─────────

        /// <summary>العمل على العروض (تسجيلها ومطابقتها واختيارها وتوثيق التوصية): للمكلَّف بالطلب (ومدير النظام) والطلب قيد جمع العروض</summary>
        public static void EnsureCanWork(Viewer viewer, PricingRequest request)
        {
            Require(viewer, AppPermissions.PreparePricingOffers, "إعداد عروض التسعير");
            EnsureInBranch(viewer, request.BranchId);
            if (!viewer.IsSuperAdmin && request.AssigneeUserId != viewer.Id) throw new UnauthorizedAccessException("هذا الطلب غير مكلَّف به");
            if (request.Status != PricingRequestStatus.Collecting)
                throw new InvalidOperationException($"الطلب «{StatusAr(request.Status)}»: " + (request.Status == PricingRequestStatus.Registered ? "كلِّف موظفاً أولاً" : "لا تُعدَّل عروضه"));
        }

        public static bool IsValidCurrency(string? code) => ContractRules.IsValidCurrency(code);

        public static string? ValidateOffer(string? offerNumber, string? currency, DateTime receivedAt, DateTime? validUntil, string? terms, string? note, DateTime today)
        {
            if ((offerNumber ?? string.Empty).Length > 100) return "رقم العرض لا يتجاوز 100 حرف";
            if (!IsValidCurrency(currency)) return "رمز العملة ثلاثة أحرف لاتينية كبيرة (مثل SYP)";
            if (receivedAt.Date > today.Date) return "تاريخ استلام العرض لا يكون في المستقبل";
            if (receivedAt.Date < today.Date.AddYears(-5)) return "تاريخ استلام العرض بعيد في الماضي";
            if (validUntil is DateTime v && v.Date < receivedAt.Date) return "صلاحية العرض لا تسبق تاريخ استلامه";
            if ((terms ?? string.Empty).Length > 2000) return "شروط التوريد لا تتجاوز 2000 حرف";
            if ((note ?? string.Empty).Length > 2000) return "الملاحظة لا تتجاوز 2000 حرف";
            return null;
        }

        /// <summary>عرض جديد: للمكلَّف، من مورد نشط من فرع الطلب لم يقدّم عرضاً، وبحدّ أقصى للعروض</summary>
        public static void EnsureCanAddOffer(Viewer viewer, PricingRequest request, Contractor supplier)
        {
            EnsureCanWork(viewer, request);
            if (request.Offers.Count >= MaxOffers) throw new InvalidOperationException($"بلغ الطلب الحد الأقصى للعروض ({MaxOffers})");
            if (request.Offers.Any(o => o.SupplierId == supplier.Id)) throw new InvalidOperationException("لهذا المورد عرض مسجَّل على الطلب");
            ContractRules.EnsureContractorUsable(supplier, request.BranchId);
        }

        /// <summary>سعر بند مقترح في عرض</summary>
        public sealed record LineInput(int ItemId, decimal UnitPrice, string? Unit, int Conformity, string? ConformityNote);

        /// <summary>
        /// أسعار البنود: البند من الطلب ولا يتكرر، وسعر الوحدة غير سالب، والوحدة المعروضة مذكورة (قد تخالف وحدة البند فيُعرض «غير قابل للمقارنة»)،
        /// والمطابقة معروفة. لا تُحتسب أي تحويلات.
        /// </summary>
        public static string? ValidateLines(PricingRequest request, IReadOnlyCollection<LineInput> lines)
        {
            if (lines.Count == 0) return "أدخل سعر بند واحد على الأقل";
            if (lines.Select(l => l.ItemId).Distinct().Count() != lines.Count) return "بند مكرر في العرض";
            foreach (var l in lines)
            {
                if (request.Items.All(i => i.Id != l.ItemId)) return "بند غير موجود في الطلب";
                if (l.UnitPrice < 0) return "سعر الوحدة لا يكون سالباً";
                if (l.UnitPrice > 1_000_000_000_000m) return "سعر الوحدة كبير جداً";
                if (string.IsNullOrWhiteSpace(l.Unit)) return "اذكر الوحدة المعروضة";
                if (l.Unit.Trim().Length > 30) return "الوحدة لا تتجاوز 30 حرفاً";
                if (!IsValidConformity(l.Conformity)) return "نتيجة المطابقة غير معروفة";
                if ((l.ConformityNote ?? string.Empty).Length > 1000) return "ملاحظة المطابقة لا تتجاوز 1000 حرف";
            }
            return null;
        }

        /// <summary>سطر مختار لا يتغير سعره ولا وحدته ولا مطابقته قبل إلغاء اختياره (ما اختير هو ما وُثّقت التوصية عليه)</summary>
        public static void EnsureLineEditable(PricingOfferLine line)
        {
            if (line.IsSelected) throw new InvalidOperationException("هذا السطر مختار: ألغِ اختياره قبل تعديله");
        }

        // ───────── المقارنة (وحدة وعملة واحدة فقط) ─────────

        private static string Norm(string? unit) => (unit ?? string.Empty).Trim().ToLowerInvariant();

        public static bool SameUnit(string? a, string? b) => Norm(a) == Norm(b);

        /// <summary>صف مقارنة: سعر بند عند مورد، مع سبب عدم القابلية للمقارنة (إن وُجد) وهل هو الأرخص بين المطابقة القابلة للمقارنة بعملته</summary>
        public sealed record ComparisonRow(PricingOffer Offer, PricingOfferLine Line, decimal Total, bool Comparable, string? NotComparableReason, bool IsLowest);

        /// <summary>
        /// مقارنة عروض بند واحد. القابل للمقارنة ما وحدته كوحدة البند؛ وتُقارَن الأسعار داخل العملة الواحدة فقط (لا تحويل عملات).
        /// «الأرخص» معلومة للعرض لا قرار: يحسبها بين السطور القابلة للمقارنة التي تحققت مطابقتها، لكل عملة على حدة.
        /// </summary>
        public static List<ComparisonRow> Compare(PricingRequestItem item, IEnumerable<PricingOffer> offers)
        {
            var rows = offers.SelectMany(o => o.Lines.Where(l => l.PricingRequestItemId == item.Id).Select(l => (Offer: o, Line: l))).ToList();
            var lowestByCurrency = rows
                .Where(r => SameUnit(r.Line.Unit, item.Unit) && r.Line.Conformity == ConformityResult.Conforming)
                .GroupBy(r => r.Offer.Currency)
                .ToDictionary(g => g.Key, g => g.Min(r => r.Line.UnitPrice));

            return rows.Select(r =>
            {
                var comparable = SameUnit(r.Line.Unit, item.Unit);
                var reason = comparable ? null : $"الوحدة المعروضة ({r.Line.Unit}) تختلف عن وحدة البند ({item.Unit}): لا تُقارَن بلا تحويل معتمد";
                var lowest = comparable && r.Line.Conformity == ConformityResult.Conforming
                    && lowestByCurrency.TryGetValue(r.Offer.Currency, out var min) && r.Line.UnitPrice == min;
                return new ComparisonRow(r.Offer, r.Line, r.Line.UnitPrice * item.Quantity, comparable, reason, lowest);
            }).OrderBy(r => r.Offer.Currency).ThenBy(r => r.Line.UnitPrice).ToList();
        }

        /// <summary>عملات الأسعار في مقارنة بند (أكثر من واحدة: تظهر مجموعات منفصلة لا تُجمع)</summary>
        public static List<string> CurrenciesIn(IEnumerable<ComparisonRow> rows) => rows.Select(r => r.Offer.Currency).Distinct().OrderBy(c => c).ToList();

        // ───────── الاختيار والتوصية ─────────

        /// <summary>
        /// اختيار سطر لبند: للمكلَّف، والسطر من هذا البند، <b>مطابق فنياً</b>، بوحدة البند نفسها، وعرضه لم تنتهِ صلاحيته، وبسبب مكتوب
        /// (حتى لو كان الأرخص: الاختيار قرار لا حساب).
        /// </summary>
        public static void EnsureCanSelect(Viewer viewer, PricingRequest request, PricingRequestItem item, PricingOffer offer, PricingOfferLine line, string? reason, DateTime today)
        {
            EnsureCanWork(viewer, request);
            if (line.PricingRequestItemId != item.Id || offer.Lines.All(l => l.Id != line.Id)) throw new InvalidOperationException("السطر ليس من هذا البند");
            if (line.Conformity != ConformityResult.Conforming) throw new InvalidOperationException("لا يُختار سطر لم تتحقق مطابقته الفنية (ولو كان الأرخص)");
            if (!SameUnit(line.Unit, item.Unit)) throw new InvalidOperationException($"الوحدة المعروضة ({line.Unit}) تختلف عن وحدة البند ({item.Unit}): لا اختيار بلا تحويل معتمد");
            if (offer.ValidUntil is DateTime v && v.Date < today.Date) throw new InvalidOperationException("انتهت صلاحية هذا العرض");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("اكتب سبب اختيار هذا العرض");
            if (reason.Length > 1000) throw new InvalidOperationException("سبب الاختيار لا يتجاوز 1000 حرف");
        }

        /// <summary>إلغاء الاختيار: للمكلَّف والطلب قيد جمع العروض</summary>
        public static void EnsureCanUnselect(Viewer viewer, PricingRequest request, PricingOfferLine line)
        {
            EnsureCanWork(viewer, request);
            if (!line.IsSelected) throw new InvalidOperationException("هذا السطر غير مختار");
        }

        /// <summary>
        /// توثيق التوصية: للمكلَّف، وللطلب عرض واحد على الأقل، ولكل بند سطر مختار، وبنص التوصية. يُحيله إلى الاعتماد.
        /// </summary>
        public static void EnsureCanRecommend(Viewer viewer, PricingRequest request, string? note)
        {
            EnsureCanWork(viewer, request);
            if (request.Offers.Count == 0) throw new InvalidOperationException("سجّل عرضاً واحداً على الأقل");
            var selected = request.Offers.SelectMany(o => o.Lines).Where(l => l.IsSelected).Select(l => l.PricingRequestItemId).ToHashSet();
            var missing = request.Items.Where(i => !selected.Contains(i.Id)).ToList();
            if (missing.Count > 0) throw new InvalidOperationException($"اختر عرضاً لكل بند قبل التوصية (ينقص {missing.Count})");
            if (string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("اكتب التوصية");
            if (note.Length > 2000) throw new InvalidOperationException("التوصية لا تتجاوز 2000 حرف");
        }

        /// <summary>مجموع المختار لكل عملة (جمع بسيط لسعر الوحدة × كمية البند؛ العملات لا تُجمع معاً)</summary>
        public static Dictionary<string, decimal> SelectedTotals(PricingRequest request) =>
            request.Offers.SelectMany(o => o.Lines.Where(l => l.IsSelected).Select(l => (o.Currency, Total: l.UnitPrice * request.Items.First(i => i.Id == l.PricingRequestItemId).Quantity)))
                .GroupBy(x => x.Currency).ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

        // ───────── الاعتماد والإرسال ─────────

        /// <summary>
        /// القرار النهائي على التوصية: لصاحب «الاعتماد» في فرع الطلب، والطلب «موصى به»، وليس من أوصى به (حتى مدير النظام).
        /// الرفض والإعادة يشترطان سبباً.
        /// </summary>
        public static void EnsureCanDecide(Viewer viewer, PricingRequest request, Decision decision, string? note)
        {
            Require(viewer, AppPermissions.ApprovePricingRequest, "اعتماد نتيجة طلب التسعير");
            EnsureInBranch(viewer, request.BranchId);
            if (request.Status != PricingRequestStatus.Recommended) throw new InvalidOperationException("الطلب ليس بانتظار الاعتماد");
            if (request.RecommendedByUserId == viewer.Id) throw new UnauthorizedAccessException("لا تعتمد توصية أعددتها: يعتمدها غيرك");
            if (!Enum.IsDefined(decision)) throw new InvalidOperationException("القرار اعتماد أو رفض أو إعادة");
            if (decision != Decision.Approve && string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("اكتب سبب الرفض أو الإعادة");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");
        }

        public static PricingRequestStatus StatusAfter(Decision decision) => decision switch
        {
            Decision.Approve => PricingRequestStatus.Approved,
            Decision.Reject => PricingRequestStatus.Rejected,
            _ => PricingRequestStatus.Collecting
        };

        /// <summary>إرسال النتيجة (المعتمدة أو المرفوضة) إلى الجهة المختصة: لصاحب «الإرسال» في فرع الطلب، بتسمية الجهة</summary>
        public static void EnsureCanSendResult(Viewer viewer, PricingRequest request, string? recipient, string? note)
        {
            Require(viewer, AppPermissions.SendPricingResult, "إرسال نتيجة طلب التسعير");
            EnsureInBranch(viewer, request.BranchId);
            if (request.Status is not (PricingRequestStatus.Approved or PricingRequestStatus.Rejected))
                throw new InvalidOperationException("تُرسَل النتيجة بعد اعتمادها أو رفضها");
            if (string.IsNullOrWhiteSpace(recipient)) throw new InvalidOperationException("اكتب الجهة التي أُرسلت إليها النتيجة");
            if (recipient.Trim().Length > 150) throw new InvalidOperationException("اسم الجهة لا يتجاوز 150 حرفاً");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");
        }

        // ───────── التنبيه (لا قرار) ─────────

        /// <summary>مرشّح للتنبيه: طلب لم ينتهِ (مسجَّل أو قيد الجمع أو بانتظار الاعتماد) وتجاوز موعده. التجاوز وحده لا يغيّر شيئاً.</summary>
        public static bool IsOverdueCandidate(PricingRequest request, DateTime today) =>
            request.Status is PricingRequestStatus.Registered or PricingRequestStatus.Collecting or PricingRequestStatus.Recommended
            && request.DueDate is DateTime due && due.Date < today.Date;

        /// <summary>تنبيه واحد لكل موعد: لم يُنبَّه عنه، أو غُيّر الموعد بعد التنبيه</summary>
        public static bool NeedsOverdueNotice(PricingRequest request) =>
            request.DueDate is DateTime due && request.OverdueNotifiedFor?.Date != due.Date;

        // ───────── محضر الاتفاق مع متعهد جديد ─────────

        /// <summary>سعر متفق عليه مقترح في محضر</summary>
        public sealed record AgreementLineInput(string? Description, decimal Quantity, string? Unit, decimal UnitPrice);

        public static string? ValidateAgreement(string? number, DateTime agreementDate, string? specs, string? method, string? currency, string? notes,
            IReadOnlyCollection<AgreementLineInput> lines, DateTime today)
        {
            if ((number ?? string.Empty).Trim().Length > 100) return "رقم المحضر لا يتجاوز 100 حرف";
            if (agreementDate.Date > today.Date) return "تاريخ المحضر لا يكون في المستقبل";
            if (agreementDate.Date < today.Date.AddYears(-5)) return "تاريخ المحضر بعيد في الماضي";
            if ((specs ?? string.Empty).Length > 4000) return "المواصفات الفنية لا تتجاوز 4000 حرف";
            if ((method ?? string.Empty).Length > 2000) return "طريقة التنفيذ لا تتجاوز 2000 حرف";
            if (!IsValidCurrency(currency)) return "رمز العملة ثلاثة أحرف لاتينية كبيرة (مثل SYP)";
            if ((notes ?? string.Empty).Length > 2000) return "الملاحظات لا تتجاوز 2000 حرف";
            if (lines.Count == 0) return "أضف سعراً متفقاً عليه واحداً على الأقل";
            if (lines.Count > MaxAgreementLines) return $"عدد البنود لا يتجاوز {MaxAgreementLines}";
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l.Description)) return "وصف البند مطلوب";
                if (l.Description.Trim().Length > 500) return "وصف البند لا يتجاوز 500 حرف";
                if (l.Quantity <= 0) return "كمية البند أكبر من صفر";
                if (string.IsNullOrWhiteSpace(l.Unit)) return "وحدة البند مطلوبة";
                if (l.Unit.Trim().Length > 30) return "وحدة البند لا تتجاوز 30 حرفاً";
                if (l.UnitPrice < 0) return "سعر الوحدة لا يكون سالباً";
            }
            return null;
        }

        /// <summary>إعداد محضر جديد: لحامل صلاحية الإعداد (مدير المكتب) في فرع المحضر، والمتعهد نشط من الفرع، والمشروع (إن حُدّد) يراه ومن الفرع نفسه</summary>
        public static void EnsureCanPrepareAgreement(Viewer viewer, int branchId, Contractor supplier, Project? project)
        {
            Require(viewer, AppPermissions.PreparePricingAgreement, "إعداد محضر اتفاق");
            EnsureInBranch(viewer, branchId);
            ContractRules.EnsureContractorUsable(supplier, branchId);
            if (project != null)
            {
                ProjectRules.EnsureView(viewer, project);
                if (project.BranchId != branchId) throw new InvalidOperationException("المشروع من فرع آخر");
            }
        }

        /// <summary>تعديل المسودة أو المعاد وإرسالها: لمن أعدّها (أو مدير النظام) بصلاحية الإعداد، والمحضر مسودة أو معاد</summary>
        public static void EnsureCanEditAgreement(Viewer viewer, PricingAgreement agreement)
        {
            Require(viewer, AppPermissions.PreparePricingAgreement, "تعديل محضر الاتفاق");
            EnsureInBranch(viewer, agreement.BranchId);
            if (!viewer.IsSuperAdmin && agreement.PreparedByUserId != viewer.Id) throw new UnauthorizedAccessException("هذا المحضر أعدّه غيرك");
            if (agreement.Status is not (AgreementStatus.Draft or AgreementStatus.Returned))
                throw new InvalidOperationException($"المحضر «{AgreementStatusAr(agreement.Status)}»: لا يُعدَّل");
        }

        /// <summary>الإرسال للاعتماد: يلزم الرقم الرسمي وبند واحد على الأقل</summary>
        public static void EnsureCanSubmitAgreement(Viewer viewer, PricingAgreement agreement)
        {
            EnsureCanEditAgreement(viewer, agreement);
            if (string.IsNullOrWhiteSpace(agreement.Number)) throw new InvalidOperationException("أدخل رقم المحضر قبل إرساله للاعتماد");
            if (agreement.Lines.Count == 0) throw new InvalidOperationException("أضف سعراً متفقاً عليه واحداً على الأقل");
        }

        /// <summary>
        /// اعتماد المحضر أو إعادته: لحامل صلاحية الاعتماد في فرعه، والمحضر مرسل، وليس من أعدّه (حتى مدير النظام). الإعادة تشترط سبباً.
        /// </summary>
        public static void EnsureCanApproveAgreement(Viewer viewer, PricingAgreement agreement, bool approve, string? note)
        {
            Require(viewer, AppPermissions.ApprovePricingAgreement, "اعتماد محضر الاتفاق");
            EnsureInBranch(viewer, agreement.BranchId);
            if (agreement.Status != AgreementStatus.Submitted) throw new InvalidOperationException("المحضر ليس بانتظار الاعتماد");
            if (viewer.Id == agreement.PreparedByUserId) throw new UnauthorizedAccessException("لا تعتمد محضراً أعددته: يعتمده غيرك");
            if (!approve && string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("اكتب سبب إعادة المحضر");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");
        }

        /// <summary>إلغاء مسودة أو معاد (بديل الحذف): لمن أعدّه؛ المرسل والمعتمد لا يُلغيان</summary>
        public static void EnsureCanCancelAgreement(Viewer viewer, PricingAgreement agreement) => EnsureCanEditAgreement(viewer, agreement);

        /// <summary>مجموع المحضر (جمع بسيط للكمية × سعر الوحدة بعملته)</summary>
        public static decimal AgreementTotal(PricingAgreement agreement) => agreement.Lines.Sum(l => l.Quantity * l.UnitPrice);
    }
}
