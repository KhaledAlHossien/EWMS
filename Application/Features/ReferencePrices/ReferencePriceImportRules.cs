using System.Globalization;
using Application.Common;
using Application.Features.Contracts;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.ReferencePrices
{
    /// <summary>
    /// استيراد الأسعار المرجعية من Excel (المرحلة 5ب.2) — قواعد صرفة:
    /// - فحص كامل قبل أي حفظ: كل سطر يُفحص بقواعد الإضافة نفسها (مصدر وتاريخ ووحدة وعملة…)، و<b>إن وُجد خطأ واحد لا يُحفظ شيء</b> (الكل أو لا شيء).
    /// - المورد باسمه تماماً من سجل مقاولي الفرع (نشط)؛ لا يُنشأ مورد تلقائياً.
    /// - التكرار خطأ: السعر نفسه (مادة ووحدة وعملة ومورد وتاريخ وسعر) مرتين في الملف أو مسجَّل مسبقاً.
    /// - الأرقام والتواريخ بصيغة صريحة (نقطة عشرية، 2026-10-05)؛ ما يحتمل أكثر من قراءة يُرفض بدل تخمينه.
    /// - كل سعر مستورد «تاريخي» ينتظر الاعتماد من غير المستورِد، ويشير إلى دفعة الاستيراد.
    /// </summary>
    public static class ReferencePriceImportRules
    {
        public const int MaxRows = 1000;

        /// <summary>سطر صالح بعد التحليل</summary>
        public sealed record ParsedPrice(int Row, string? ItemCode, string ItemName, string Specifications, string Unit, int SupplierId, string SupplierName,
            decimal UnitPrice, string Currency, DateTime PriceDate, string SourceDocument, string Notes);

        /// <summary>نتيجة فحص سطر: السعر المحلَّل (إن صلح) وأخطاؤه</summary>
        public sealed class RowResult(ReferencePriceSheetRow row)
        {
            public ReferencePriceSheetRow Row { get; } = row;
            public ParsedPrice? Price { get; set; }
            public List<string> Errors { get; } = [];
            public bool Valid => Errors.Count == 0 && Price != null;
        }

        /// <summary>اسم المورد للمطابقة: بلا فروق في المسافات أو حالة الأحرف</summary>
        public static string NormName(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        public static void EnsureCanImport(Viewer viewer, int branchId)
        {
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ImportReferencePrices))
                throw new UnauthorizedAccessException("لا تملك صلاحية استيراد الأسعار المرجعية");
            if (!ReferencePriceRules.InBranch(viewer, branchId)) throw new UnauthorizedAccessException("هذا الفرع ليس فرعك");
        }

        // ───────── التحليل ─────────

        private static string Latin(string value) => new(value.Select(c => c switch
        {
            >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
            >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
            '٫' => '.',
            _ => c
        }).ToArray());

        /// <summary>رقم بنقطة عشرية؛ الفاصلة (آلاف أو عشرية) تُرفض لاحتمالها قراءتين</summary>
        public static bool TryParsePrice(string? text, out decimal price, out string? problem)
        {
            price = 0m; problem = null;
            var value = Latin((text ?? string.Empty).Trim()).Replace(" ", string.Empty);
            if (value.Length == 0) { problem = "سعر الوحدة مطلوب"; return false; }
            if (value.Contains(',') || value.Contains('٬')) { problem = "اكتب السعر بنقطة عشرية وبلا فواصل آلاف (مثل 1250.5)"; return false; }
            if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out price)) { problem = "سعر الوحدة ليس رقماً صحيحاً"; return false; }
            return true;
        }

        /// <summary>تاريخ بصيغة 2026-10-05 أو 2026/10/05 فقط (الصيغ اليومية/الشهرية ملتبسة فتُرفض)</summary>
        public static bool TryParseDate(string? text, out DateTime date)
        {
            var value = Latin((text ?? string.Empty).Trim());
            return DateTime.TryParseExact(value.Length >= 10 ? value[..10] : value, ["yyyy-MM-dd", "yyyy/MM/dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        /// <summary>يفحص سطراً: تحليل الأرقام والتواريخ، المورد، ثم قواعد الإضافة نفسها (المصدر الاستيراد)</summary>
        public static RowResult ValidateRow(ReferencePriceSheetRow row, IReadOnlyDictionary<string, Contractor> suppliers, DateTime today)
        {
            var result = new RowResult(row);

            var priceOk = TryParsePrice(row.UnitPrice, out var price, out var priceProblem);
            if (!priceOk) result.Errors.Add(priceProblem!);
            var dateOk = TryParseDate(row.PriceDate, out var date);
            if (!dateOk) result.Errors.Add(string.IsNullOrWhiteSpace(row.PriceDate) ? "تاريخ السعر مطلوب" : "التاريخ بصيغة 2026-10-05");

            Contractor? supplier = null;
            if (string.IsNullOrWhiteSpace(row.Supplier)) result.Errors.Add("المورد مطلوب");
            else if (!suppliers.TryGetValue(NormName(row.Supplier), out supplier)) result.Errors.Add($"المورد «{row.Supplier.Trim()}» غير موجود في سجل الفرع — أضفه أولاً بالاسم نفسه تماماً");
            else if (!supplier.IsActive) { result.Errors.Add($"المورد «{supplier.Name}» معطَّل"); supplier = null; }

            var currency = row.Currency.Trim().ToUpperInvariant();
            if (priceOk && dateOk && ReferencePriceRules.ValidateInput(row.ItemCode, row.ItemName, row.Specifications, row.Unit, price, currency, date,
                    (int)ReferencePriceSource.Import, row.SourceDocument, row.Notes, today, allowImportSource: true) is string problem)
                result.Errors.Add(problem);

            if (result.Errors.Count == 0 && supplier != null)
                result.Price = new ParsedPrice(row.Row, string.IsNullOrWhiteSpace(row.ItemCode) ? null : row.ItemCode.Trim(), row.ItemName.Trim(), row.Specifications.Trim(), row.Unit.Trim(),
                    supplier.Id, supplier.Name, price, currency, date.Date, row.SourceDocument.Trim(), row.Notes.Trim());
            return result;
        }

        /// <summary>مفتاح التطابق: المادة (رمز أو اسم) ووحدتها وعملتها ثم المورد والتاريخ والسعر — السعر نفسه</summary>
        public static string DuplicateKey(string? itemCode, string? itemName, string? unit, string? currency, int supplierId, DateTime date, decimal price) =>
            $"{ReferencePriceRules.Key(itemCode, itemName, unit, currency)}|{supplierId}|{date:yyyy-MM-dd}|{price.ToString("0.####", CultureInfo.InvariantCulture)}";

        public static string DuplicateKey(ParsedPrice p) => DuplicateKey(p.ItemCode, p.ItemName, p.Unit, p.Currency, p.SupplierId, p.PriceDate, p.UnitPrice);

        public static string DuplicateKey(ReferencePrice p) => DuplicateKey(p.ItemCode, p.ItemName, p.Unit, p.Currency, p.SupplierId, p.PriceDate, p.UnitPrice);

        /// <summary>التكرار داخل الملف: السطر المكرر (الثاني فصاعداً) خطأ يشير إلى أول سطر</summary>
        public static void FlagFileDuplicates(IReadOnlyList<RowResult> results)
        {
            var seen = new Dictionary<string, int>();
            foreach (var r in results.Where(r => r.Price != null))
            {
                var key = DuplicateKey(r.Price!);
                if (seen.TryGetValue(key, out var first)) r.Errors.Add($"السعر نفسه مكرر في الملف (السطر {first})");
                else seen[key] = r.Price!.Row;
            }
        }

        /// <summary>التكرار مع المسجَّل: السعر نفسه موجود مسبقاً (غير المُستبدَل)</summary>
        public static void FlagExistingDuplicates(IReadOnlyList<RowResult> results, IEnumerable<ReferencePrice> existing)
        {
            var known = new Dictionary<string, ReferencePrice>();
            foreach (var e in existing) known.TryAdd(DuplicateKey(e), e);
            foreach (var r in results.Where(r => r.Valid))
                if (known.TryGetValue(DuplicateKey(r.Price!), out var found))
                    r.Errors.Add($"السعر نفسه مسجَّل مسبقاً ({ReferencePriceRules.Number(found.Id, found.CreatedAt)})");
        }
    }
}
