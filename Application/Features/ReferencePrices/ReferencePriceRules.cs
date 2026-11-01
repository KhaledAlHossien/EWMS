using Application.Common;
using Application.Features.Contracts;
using Application.Features.Projects;
using Domain.Entities;

namespace Application.Features.ReferencePrices
{
    /// <summary>
    /// الأسعار المرجعية (المرحلة 5ب.1، 2026-11-01) — مصدر واحد للقواعد الصرفة (بلا قاعدة بيانات):
    /// - سجل تاريخي لا يُعدَّل سعره ولا يُحذف ([ADR-0007]): التصحيح سجل جديد يشير إلى المُصحَّح فيصير «مُستبدَلاً» ويبقى في التاريخ.
    /// - الإضافة غير الاعتماد: من أضاف السعر لا يعتمده «للاستخدام الحالي» حتى مدير النظام ([ADR-0008]).
    /// - «آخر سعر» هو الأحدث تاريخاً بين غير المُستبدَل لمادة بوحدة وعملة واحدة — لا الأعلى ولا الأقل؛ ولا مقارنة بين وحدات أو عملات مختلفة.
    /// - لا سعر بلا مصدر وتاريخ. الحدّ فرع السجل (ونظرة المؤسسة كل الفروع).
    /// </summary>
    public static class ReferencePriceRules
    {
        public const int MaxCodeLength = 50;
        public const int MaxNameLength = 200;
        public const int MaxUnitLength = 30;
        public const int MaxSourceLength = 200;

        // ───────── الأسماء ─────────

        /// <summary>الرقم الداخلي REF-{سنة الإنشاء}-{id:D5} — يُحسب ولا يُخزَّن</summary>
        public static string Number(int id, DateTime createdAt) => $"REF-{createdAt.Year}-{id:D5}";

        public static string StatusAr(ReferencePriceStatus status) => status switch
        {
            ReferencePriceStatus.Historical => "تاريخي",
            ReferencePriceStatus.ApprovedForCurrentUse => "معتمد للاستخدام الحالي",
            ReferencePriceStatus.Superseded => "مُستبدَل بتصحيح",
            _ => status.ToString()
        };

        public static string SourceAr(ReferencePriceSource source) => source switch
        {
            ReferencePriceSource.Manual => "إدخال يدوي",
            ReferencePriceSource.Offer => "عرض مورد أو مستند",
            ReferencePriceSource.Import => "استيراد Excel",
            _ => source.ToString()
        };

        /// <summary>المصدر المقبول من الواجهة: يدوي أو عرض/مستند؛ والاستيراد لا يُدَّعى من نموذج الإضافة</summary>
        public static bool IsValidSource(int value) => value is (int)ReferencePriceSource.Manual or (int)ReferencePriceSource.Offer;

        // ───────── الصلاحية والرؤية ─────────

        private static void Require(Viewer viewer, string permission, string action)
        {
            if (!viewer.IsSuperAdmin && !viewer.Has(permission))
                throw new UnauthorizedAccessException($"لا تملك صلاحية {action}");
        }

        public static bool InBranch(Viewer viewer, int branchId) => viewer.IsOrganizationWide || OrganizationRole.InBranch(viewer.User, branchId);

        private static void EnsureInBranch(Viewer viewer, int branchId)
        {
            if (!InBranch(viewer, branchId)) throw new UnauthorizedAccessException("هذا السعر من فرع آخر");
        }

        /// <summary>من لا يملك أي صلاحية أسعار مرجعية لا يرى شيئاً، ثم فرع السعر</summary>
        public static void EnsureCanView(Viewer viewer, ReferencePrice price)
        {
            EnsureCanViewAny(viewer);
            EnsureInBranch(viewer, price.BranchId);
        }

        public static void EnsureCanViewAny(Viewer viewer)
        {
            if (!AppPermissions.AnyOf["AnyReferencePriceView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض الأسعار المرجعية");
        }

        // ───────── المدخلات ─────────

        public static string? ValidateInput(string? itemCode, string? itemName, string? specifications, string? unit, decimal unitPrice, string? currency,
            DateTime priceDate, int source, string? sourceDocument, string? notes, DateTime today, bool allowImportSource = false)
        {
            if ((itemCode ?? string.Empty).Trim().Length > MaxCodeLength) return $"رمز المادة لا يتجاوز {MaxCodeLength} حرفاً";
            if (string.IsNullOrWhiteSpace(itemName)) return "اسم المادة أو المنتج مطلوب";
            if (itemName.Trim().Length > MaxNameLength) return $"اسم المادة لا يتجاوز {MaxNameLength} حرف";
            if ((specifications ?? string.Empty).Length > 2000) return "المواصفات لا تتجاوز 2000 حرف";
            if (string.IsNullOrWhiteSpace(unit)) return "وحدة السعر مطلوبة";
            if (unit.Trim().Length > MaxUnitLength) return $"الوحدة لا تتجاوز {MaxUnitLength} حرفاً";
            if (unitPrice < 0) return "سعر الوحدة لا يكون سالباً";
            if (unitPrice > 1_000_000_000_000m) return "سعر الوحدة كبير جداً";
            if (!ContractRules.IsValidCurrency(currency)) return "رمز العملة ثلاثة أحرف لاتينية كبيرة (مثل SYP)";
            if (priceDate.Date > today.Date) return "تاريخ السعر لا يكون في المستقبل";
            if (priceDate.Date < today.Date.AddYears(-20)) return "تاريخ السعر بعيد في الماضي";
            var validSource = IsValidSource(source) || (allowImportSource && source == (int)ReferencePriceSource.Import);
            if (!validSource) return "مصدر السعر غير معروف";
            if (string.IsNullOrWhiteSpace(sourceDocument)) return "اكتب مصدر السعر (رقم عرض السعر أو وصف المستند): لا سعر بلا مصدر";
            if (sourceDocument.Trim().Length > MaxSourceLength) return $"مصدر السعر لا يتجاوز {MaxSourceLength} حرف";
            if ((notes ?? string.Empty).Length > 2000) return "الملاحظات لا تتجاوز 2000 حرف";
            return null;
        }

        /// <summary>إضافة سعر: لحامل صلاحية الإضافة في فرع السجل، والمورد نشط من الفرع، والمشروع (إن حُدّد) يراه ومن الفرع نفسه</summary>
        public static void EnsureCanAdd(Viewer viewer, int branchId, Contractor supplier, Project? project)
        {
            Require(viewer, AppPermissions.AddReferencePrice, "إضافة سعر مرجعي");
            EnsureInBranch(viewer, branchId);
            ContractRules.EnsureContractorUsable(supplier, branchId);
            if (project != null)
            {
                ProjectRules.EnsureView(viewer, project);
                if (project.BranchId != branchId) throw new InvalidOperationException("المشروع من فرع آخر");
            }
        }

        /// <summary>
        /// تصحيح سعر: بسجل جديد لحامل صلاحية الإضافة في فرع السعر، والمُصحَّح ليس مُستبدَلاً أصلاً، وسبب التصحيح مكتوب.
        /// </summary>
        public static void EnsureCanCorrect(Viewer viewer, ReferencePrice price, string? reason)
        {
            Require(viewer, AppPermissions.AddReferencePrice, "تصحيح سعر مرجعي");
            EnsureInBranch(viewer, price.BranchId);
            if (price.Status == ReferencePriceStatus.Superseded) throw new InvalidOperationException("هذا السعر مُستبدَل بتصحيح سابق: صحّح السجل الأحدث");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("اكتب سبب التصحيح");
            if (reason.Trim().Length > 1000) throw new InvalidOperationException("سبب التصحيح لا يتجاوز 1000 حرف");
        }

        /// <summary>
        /// حال المُصحَّح بعد التصحيح: التاريخي يصير «مُستبدَلاً» فوراً (خطأ إدخال)، والمعتمد يبقى معتمداً إلى أن يُعتمد بديله
        /// (فلا تُسحب أسعار معتمدة قبل أن يقرّر غير المُصحِّح).
        /// </summary>
        public static ReferencePriceStatus StatusAfterCorrection(ReferencePriceStatus status) =>
            status == ReferencePriceStatus.Historical ? ReferencePriceStatus.Superseded : status;

        /// <summary>
        /// اعتماد سعر «للاستخدام الحالي»: لحامل صلاحية الاعتماد في فرعه، والسعر تاريخي، وليس من أضافه (حتى مدير النظام).
        /// </summary>
        public static void EnsureCanApprove(Viewer viewer, ReferencePrice price, string? note)
        {
            Require(viewer, AppPermissions.ApproveReferencePrice, "اعتماد سعر مرجعي");
            EnsureInBranch(viewer, price.BranchId);
            if (price.Status != ReferencePriceStatus.Historical) throw new InvalidOperationException($"السعر «{StatusAr(price.Status)}»: لا يُعتمد");
            if (viewer.Id == price.CreatedByUserId) throw new UnauthorizedAccessException("لا تعتمد سعراً أضفته: يعتمده غيرك");
            if ((note ?? string.Empty).Length > 1000) throw new InvalidOperationException("الملاحظة لا تتجاوز 1000 حرف");
        }

        // ───────── مفتاح المادة و«آخر سعر» ─────────

        private static string Norm(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        /// <summary>مفتاح التجميع: رمز المادة إن وُجد وإلا اسمها، مع الوحدة والعملة — فلا يختلط سعران بوحدتين أو عملتين</summary>
        public static string Key(string? itemCode, string? itemName, string? unit, string? currency)
        {
            var item = string.IsNullOrWhiteSpace(itemCode) ? $"n:{Norm(itemName)}" : $"c:{Norm(itemCode)}";
            return $"{item}|{Norm(unit)}|{(currency ?? string.Empty).Trim().ToUpperInvariant()}";
        }

        public static string Key(ReferencePrice price) => Key(price.ItemCode, price.ItemName, price.Unit, price.Currency);

        /// <summary>
        /// «آخر سعر»: الأحدث تاريخاً (ثم الأحدث تسجيلاً) بين غير المُستبدَل لكل مادة بوحدة وعملة، وللمورد الواحد منها. معلومة تُعرض
        /// ولا يُبنى عليها اختيار آلي، ولا تُختار بالأعلى أو الأقل.
        /// </summary>
        public static (HashSet<int> Latest, HashSet<int> LatestForSupplier) Latest(IEnumerable<ReferencePrice> prices)
        {
            var live = prices.Where(p => p.Status != ReferencePriceStatus.Superseded).ToList();
            var latest = live.GroupBy(Key).Select(g => g.OrderByDescending(p => p.PriceDate).ThenByDescending(p => p.Id).First().Id).ToHashSet();
            var perSupplier = live.GroupBy(p => (Key(p), p.SupplierId)).Select(g => g.OrderByDescending(p => p.PriceDate).ThenByDescending(p => p.Id).First().Id).ToHashSet();
            return (latest, perSupplier);
        }
    }
}
