namespace Application.Interfaces
{
    /// <summary>سطر مقروء من ملف استيراد الأسعار المرجعية: كل الحقول نصوص كما في الملف (التحقق في القواعد الصرفة)</summary>
    public sealed class ReferencePriceSheetRow
    {
        public int Row { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Specifications { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string PriceDate { get; set; } = string.Empty;
        public string SourceDocument { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>ملفات Excel للأسعار المرجعية (ClosedXML في Infrastructure)</summary>
    public interface IReferencePriceSpreadsheet
    {
        /// <summary>قالب الاستيراد: الأعمدة بالترتيب مع سطر مثال وورقة تعليمات</summary>
        byte[] Template();

        /// <summary>يقرأ أسطر الورقة الأولى (يتخطى الفارغة وسطر المثال) — InvalidOperationException برسالة عربية إن لم يُقرأ الملف أو نقصت أعمدة مطلوبة</summary>
        List<ReferencePriceSheetRow> Read(Stream file, int maxRows);
    }
}
