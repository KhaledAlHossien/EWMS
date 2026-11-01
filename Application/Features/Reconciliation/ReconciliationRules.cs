using Application.Common;
using Application.Features.Contracts;
using Application.Features.Projects;
using Domain.Entities;

namespace Application.Features.Reconciliation
{
    /// <summary>
    /// مطابقة الفواتير والكشوف مع الأسعار (المرحلة 5ب.3) — قواعد صرفة:
    /// - تُوثَّق الفروق في السعر والكمية بحالات ثمانٍ معتمدة بدل «مطابق/غير مطابق» فقط. النظام يقارن ويقترح ولا يقرر.
    /// - لا مقارنة بين وحدات أو عملات مختلفة ([ADR-0019]): البند بوحدة أو عملة مختلفة «غير قابل للمقارنة»، وبلا مرجع «بيانات غير مكتملة».
    /// - المرجع (سعر وكمية ووحدة وعملة) يُجمَّد عند الربط ولا يتغير بتغير السعر المرجعي لاحقاً ([ADR-0007]).
    /// - المدقق يسجّل إحدى ست نتائج، ويقرر غيره الاعتماد أو الإعادة (حتى مدير النظام لا يعتمد ما دقّقه) ([ADR-0008]). المعتمد لا يُعدَّل.
    /// </summary>
    public static class ReconciliationRules
    {
        public const int MaxLines = 200;

        // ───────── الأسماء ─────────

        /// <summary>الرقم الداخلي MAT-{سنة الإنشاء}-{id:D5}</summary>
        public static string Number(int id, DateTime createdAt) => $"MAT-{createdAt.Year}-{id:D5}";

        public static string KindAr(ReconciliationDocumentKind kind) => kind switch
        {
            ReconciliationDocumentKind.Invoice => "فاتورة",
            ReconciliationDocumentKind.Statement => "كشف",
            _ => kind.ToString()
        };

        public static string StatusAr(ReconciliationStatus status) => status switch
        {
            ReconciliationStatus.Draft => "مسودة",
            ReconciliationStatus.Match => "مطابق",
            ReconciliationStatus.PartialMatch => "مطابق جزئياً",
            ReconciliationStatus.PriceDifference => "يوجد فرق في السعر",
            ReconciliationStatus.QuantityDifference => "يوجد فرق في الكمية",
            ReconciliationStatus.IncompleteData => "بيانات غير مكتملة",
            ReconciliationStatus.NeedsReview => "يحتاج إلى مراجعة",
            ReconciliationStatus.ApprovedAfterAudit => "معتمد بعد التدقيق",
            ReconciliationStatus.ReturnedForCorrection => "معاد للتصحيح",
            ReconciliationStatus.Cancelled => "ملغى",
            _ => status.ToString()
        };

        /// <summary>النتائج التي يسجّلها المدقق (الست الأولى)</summary>
        public static bool IsAuditResult(ReconciliationStatus status) =>
            status is ReconciliationStatus.Match or ReconciliationStatus.PartialMatch or ReconciliationStatus.PriceDifference
                or ReconciliationStatus.QuantityDifference or ReconciliationStatus.IncompleteData or ReconciliationStatus.NeedsReview;

        public static bool IsValidKind(int value) => Enum.IsDefined(typeof(ReconciliationDocumentKind), value);

        // ───────── الصلاحية والرؤية ─────────

        private static void Require(Viewer viewer, string permission, string action)
        {
            if (!viewer.IsSuperAdmin && !viewer.Has(permission))
                throw new UnauthorizedAccessException($"لا تملك صلاحية {action}");
        }

        public static bool InBranch(Viewer viewer, int branchId) => viewer.IsOrganizationWide || OrganizationRole.InBranch(viewer.User, branchId);

        private static void EnsureInBranch(Viewer viewer, int branchId)
        {
            if (!InBranch(viewer, branchId)) throw new UnauthorizedAccessException("هذه المطابقة من فرع آخر");
        }

        public static void EnsureCanViewAny(Viewer viewer)
        {
            if (!AppPermissions.AnyOf["AnyPriceReconciliationView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض مطابقات الفواتير");
        }

        public static void EnsureCanView(Viewer viewer, PriceReconciliation reconciliation)
        {
            EnsureCanViewAny(viewer);
            EnsureInBranch(viewer, reconciliation.BranchId);
        }

        // ───────── المدخلات ─────────

        /// <summary>بند مقترح: ما في المستند، ومرجعه (سطر عرض أو سعر مرجعي) إن حُدّد</summary>
        public sealed record LineInput(string? Description, string? Unit, decimal Quantity, decimal UnitPrice, int ReferenceKind, int? ReferenceId, string? Note);

        public static string? ValidateDocument(int kind, string? number, DateTime date, string? currency, string? notes, IReadOnlyCollection<LineInput> lines, DateTime today)
        {
            if (!IsValidKind(kind)) return "نوع المستند غير معروف";
            if (string.IsNullOrWhiteSpace(number)) return "رقم المستند مطلوب";
            if (number.Trim().Length > 100) return "رقم المستند لا يتجاوز 100 حرف";
            if (date.Date > today.Date) return "تاريخ المستند لا يكون في المستقبل";
            if (date.Date < today.Date.AddYears(-10)) return "تاريخ المستند بعيد في الماضي";
            if (!ContractRules.IsValidCurrency(currency)) return "رمز العملة ثلاثة أحرف لاتينية كبيرة (مثل SYP)";
            if ((notes ?? string.Empty).Length > 2000) return "الملاحظات لا تتجاوز 2000 حرف";
            if (lines.Count == 0) return "أضف بنداً واحداً على الأقل";
            if (lines.Count > MaxLines) return $"عدد البنود لا يتجاوز {MaxLines}";
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l.Description)) return "وصف البند مطلوب";
                if (l.Description.Trim().Length > 500) return "وصف البند لا يتجاوز 500 حرف";
                if (string.IsNullOrWhiteSpace(l.Unit)) return "وحدة البند مطلوبة";
                if (l.Unit.Trim().Length > 30) return "وحدة البند لا تتجاوز 30 حرفاً";
                if (l.Quantity <= 0) return "كمية البند في المستند أكبر من صفر";
                if (l.UnitPrice < 0) return "سعر الوحدة في المستند لا يكون سالباً";
                if (l.UnitPrice > 1_000_000_000_000m) return "سعر الوحدة كبير جداً";
                if (!Enum.IsDefined(typeof(ReconciliationReferenceKind), l.ReferenceKind)) return "نوع المرجع غير معروف";
                if ((ReconciliationReferenceKind)l.ReferenceKind != ReconciliationReferenceKind.None && l.ReferenceId == null) return "حدّد المرجع المختار";
                if ((l.Note ?? string.Empty).Length > 1000) return "ملاحظة البند لا تتجاوز 1000 حرف";
            }
            return null;
        }

        /// <summary>تسجيل مستند: لحامل صلاحية التدقيق في فرعه، والمورد نشط من الفرع، والمشروع (إن حُدّد) يراه ومن الفرع نفسه</summary>
        public static void EnsureCanPrepare(Viewer viewer, int branchId, Contractor supplier, Project? project)
        {
            Require(viewer, AppPermissions.PreparePriceReconciliation, "تسجيل مطابقة فاتورة");
            EnsureInBranch(viewer, branchId);
            ContractRules.EnsureContractorUsable(supplier, branchId);
            if (project != null)
            {
                ProjectRules.EnsureView(viewer, project);
                if (project.BranchId != branchId) throw new InvalidOperationException("المشروع من فرع آخر");
            }
        }

        /// <summary>تعديل المسودة أو المعاد وتسجيل نتيجتها: لمن دقّقها (أو مدير النظام) بصلاحية التدقيق</summary>
        public static void EnsureCanEdit(Viewer viewer, PriceReconciliation reconciliation)
        {
            Require(viewer, AppPermissions.PreparePriceReconciliation, "تعديل مطابقة فاتورة");
            EnsureInBranch(viewer, reconciliation.BranchId);
            if (!viewer.IsSuperAdmin && reconciliation.PreparedByUserId != viewer.Id) throw new UnauthorizedAccessException("هذه المطابقة دقّقها غيرك");
            if (reconciliation.Status is not (ReconciliationStatus.Draft or ReconciliationStatus.ReturnedForCorrection))
                throw new InvalidOperationException($"المطابقة «{StatusAr(reconciliation.Status)}»: لا تُعدَّل");
        }

        /// <summary>تسجيل النتيجة: إحدى الست، وللمطابق وحده تُعفى الملاحظة، وكل بند له مرجع أو يُسجَّل «بيانات غير مكتملة»</summary>
        public static void EnsureCanRecord(Viewer viewer, PriceReconciliation reconciliation, ReconciliationStatus result, string? note)
        {
            EnsureCanEdit(viewer, reconciliation);
            if (!IsAuditResult(result)) throw new InvalidOperationException("نتيجة التدقيق غير معروفة");
            if (reconciliation.Lines.Count == 0) throw new InvalidOperationException("أضف بنداً واحداً على الأقل");
            if (result != ReconciliationStatus.Match && string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("اكتب ملاحظات المدقق (سبب النتيجة)");
            if ((note ?? string.Empty).Length > 2000) throw new InvalidOperationException("ملاحظات المدقق لا تتجاوز 2000 حرف");
            if (result == ReconciliationStatus.Match)
            {
                var suggested = Suggest(reconciliation);
                if (suggested != ReconciliationStatus.Match) throw new InvalidOperationException($"لا يُسجَّل «مطابق» والمقارنة تقترح «{StatusAr(suggested)}»: سجّل الحالة الأدق أو صحّح البنود");
            }
        }

        /// <summary>الاعتماد أو الإعادة: لحامل صلاحية الاعتماد في فرعه، والنتيجة مسجَّلة، وليس من دقّقها (حتى مدير النظام). الإعادة تشترط سبباً</summary>
        public static void EnsureCanDecide(Viewer viewer, PriceReconciliation reconciliation, bool approve, string? note)
        {
            Require(viewer, AppPermissions.ApprovePriceReconciliation, "اعتماد مطابقة فاتورة");
            EnsureInBranch(viewer, reconciliation.BranchId);
            if (!IsAuditResult(reconciliation.Status)) throw new InvalidOperationException("المطابقة ليست بانتظار الاعتماد");
            if (viewer.Id == reconciliation.PreparedByUserId) throw new UnauthorizedAccessException("لا تعتمد مطابقة دقّقتها: يعتمدها غيرك");
            if (!approve && string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("اكتب سبب إعادة المطابقة للتصحيح");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");
        }

        /// <summary>الإحالة إلى الجهة المختصة: لمن يعتمد، بعد الاعتماد، مرة واحدة، بجهة مكتوبة</summary>
        public static void EnsureCanRefer(Viewer viewer, PriceReconciliation reconciliation, string? recipient)
        {
            Require(viewer, AppPermissions.ApprovePriceReconciliation, "إحالة مطابقة فاتورة");
            EnsureInBranch(viewer, reconciliation.BranchId);
            if (reconciliation.Status != ReconciliationStatus.ApprovedAfterAudit) throw new InvalidOperationException("تُحال المطابقة بعد اعتمادها");
            if (reconciliation.ReferredAt != null) throw new InvalidOperationException("أُحيلت هذه المطابقة سابقاً");
            if (string.IsNullOrWhiteSpace(recipient)) throw new InvalidOperationException("اكتب الجهة المختصة");
            if (recipient.Trim().Length > 200) throw new InvalidOperationException("اسم الجهة لا يتجاوز 200 حرف");
        }

        /// <summary>إلغاء مسودة أو معاد (بديل الحذف): لمن دقّقه؛ المسجَّلة والمعتمدة لا تُلغى</summary>
        public static void EnsureCanCancel(Viewer viewer, PriceReconciliation reconciliation) => EnsureCanEdit(viewer, reconciliation);

        // ───────── المقارنة ─────────

        private static string Norm(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        /// <summary>مقارنة بند: قابلة فقط لمرجع بالوحدة والعملة نفسهما، وفرق الكمية متى عُرفت كمية المرجع</summary>
        public sealed record LineComparison(bool HasReference, bool Comparable, string? NotComparableReason, decimal? PriceDifference, decimal? QuantityDifference)
        {
            public bool HasPriceDifference => PriceDifference is decimal p && p != 0m;
            public bool HasQuantityDifference => QuantityDifference is decimal q && q != 0m;
        }

        public static LineComparison Compare(PriceReconciliationLine line, string documentCurrency)
        {
            if (line.ReferenceKind == ReconciliationReferenceKind.None || line.ReferenceUnitPrice == null)
                return new LineComparison(false, false, "لا مرجع للسعر: اربط البند بسعر معتمد", null, null);
            if (Norm(line.ReferenceUnit) != Norm(line.Unit))
                return new LineComparison(true, false, $"وحدة المرجع ({line.ReferenceUnit}) تختلف عن وحدة المستند ({line.Unit}): لا تُقارَن بلا تحويل معتمد", null, null);
            if (!string.Equals(line.ReferenceCurrency, documentCurrency, StringComparison.OrdinalIgnoreCase))
                return new LineComparison(true, false, $"عملة المرجع ({line.ReferenceCurrency}) تختلف عن عملة المستند ({documentCurrency}): لا تُقارَن", null, null);
            return new LineComparison(true, true, null, line.InvoiceUnitPrice - line.ReferenceUnitPrice.Value,
                line.ReferenceQuantity is decimal q ? line.InvoiceQuantity - q : null);
        }

        /// <summary>
        /// النتيجة المقترحة (معلومة لا قرار): بند بلا مرجع أو غير قابل للمقارنة ← بيانات غير مكتملة؛ لا فروق ← مطابق؛ فروق في بعض البنود ← مطابق جزئياً؛
        /// فروق في كلها: سعر فقط ← فرق سعر، كمية فقط ← فرق كمية، الاثنان ← يحتاج مراجعة.
        /// </summary>
        public static ReconciliationStatus Suggest(PriceReconciliation reconciliation)
        {
            var comparisons = reconciliation.Lines.Select(l => Compare(l, reconciliation.Currency)).ToList();
            if (comparisons.Count == 0 || comparisons.Any(c => !c.Comparable)) return ReconciliationStatus.IncompleteData;
            var differing = comparisons.Where(c => c.HasPriceDifference || c.HasQuantityDifference).ToList();
            if (differing.Count == 0) return ReconciliationStatus.Match;
            if (differing.Count < comparisons.Count) return ReconciliationStatus.PartialMatch;
            var price = differing.Any(c => c.HasPriceDifference);
            var quantity = differing.Any(c => c.HasQuantityDifference);
            return price && quantity ? ReconciliationStatus.NeedsReview : price ? ReconciliationStatus.PriceDifference : ReconciliationStatus.QuantityDifference;
        }

        /// <summary>إجمالي المستند (جمع بسيط للكمية × سعر الوحدة بعملته)</summary>
        public static decimal InvoiceTotal(PriceReconciliation reconciliation) => reconciliation.Lines.Sum(l => l.InvoiceQuantity * l.InvoiceUnitPrice);
    }
}
