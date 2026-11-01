using System.Globalization;
using Application.Interfaces;
using ClosedXML.Excel;

namespace Infrastructure.Files
{
    /// <summary>
    /// ملف Excel لاستيراد الأسعار المرجعية (ClosedXML): القالب وقراءة الملف. الأعمدة تُقرأ بأسماء العناوين (لا بترتيبها)،
    /// وكل خلية تُقرأ نصاً كما كُتبت (الأرقام بنقطة عشرية ثابتة والتواريخ بصيغة 2026-10-05) ليتولى التحقق قواعد الاستيراد الصرفة.
    /// </summary>
    public class ReferencePriceSpreadsheet : IReferencePriceSpreadsheet
    {
        // عنوان العمود ← الحقل (القالب يكتبها بهذا الترتيب؛ Required = مطلوب)
        private static readonly (string Header, Func<ReferencePriceSheetRow, string> Get, Action<ReferencePriceSheetRow, string> Set, bool Required)[] Columns =
        [
            ("رمز المادة", r => r.ItemCode, (r, v) => r.ItemCode = v, false),
            ("اسم المادة", r => r.ItemName, (r, v) => r.ItemName = v, true),
            ("المواصفات", r => r.Specifications, (r, v) => r.Specifications = v, false),
            ("الوحدة", r => r.Unit, (r, v) => r.Unit = v, true),
            ("المورد", r => r.Supplier, (r, v) => r.Supplier = v, true),
            ("سعر الوحدة", r => r.UnitPrice, (r, v) => r.UnitPrice = v, true),
            ("العملة", r => r.Currency, (r, v) => r.Currency = v, true),
            ("تاريخ السعر", r => r.PriceDate, (r, v) => r.PriceDate = v, true),
            ("مصدر السعر", r => r.SourceDocument, (r, v) => r.SourceDocument = v, true),
            ("ملاحظات", r => r.Notes, (r, v) => r.Notes = v, false),
        ];

        private const string SheetName = "الأسعار";
        private const string ExampleMark = "مثال — احذف هذا السطر";

        private static byte[] Save(XLWorkbook workbook)
        {
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] Template()
        {
            using var workbook = new XLWorkbook { RightToLeft = true };
            var sheet = workbook.Worksheets.Add(SheetName);
            sheet.RightToLeft = true;
            for (var c = 0; c < Columns.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = Columns[c].Header;
                sheet.Column(c + 1).Style.NumberFormat.Format = "@";   // كل الأعمدة نصية: الرمز والتاريخ يُكتبان كما هما
            }
            var header = sheet.Range(1, 1, 1, Columns.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");
            header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            for (var c = 0; c < Columns.Length; c++)
                if (Columns[c].Required) sheet.Cell(1, c + 1).Style.Font.FontColor = XLColor.FromHtml("#B42318");

            string[] example = ["CU-25", "سلك نحاس 2.5 ملم", "مجدول", "م", "اسم مورد موجود في السجل", "1250.5", "SYP", "2026-10-05", "عرض السعر رقم 7", ExampleMark];
            for (var c = 0; c < example.Length; c++) sheet.Cell(2, c + 1).Value = example[c];
            sheet.Range(2, 1, 2, Columns.Length).Style.Font.FontColor = XLColor.Gray;
            sheet.SheetView.FreezeRows(1);
            sheet.Columns().AdjustToContents(8, 40);

            var help = workbook.Worksheets.Add("تعليمات");
            help.RightToLeft = true;
            string[] lines =
            [
                "تعليمات استيراد الأسعار المرجعية",
                "• الأعمدة الحمراء مطلوبة: اسم المادة، الوحدة، المورد، سعر الوحدة، العملة، تاريخ السعر، مصدر السعر.",
                "• المورد باسمه تماماً كما في سجل المقاولين والموردين للفرع (نشط)؛ لا يُنشأ مورد تلقائياً.",
                "• سعر الوحدة رقم بنقطة عشرية وبلا فواصل آلاف (مثل 1250.5). العملة ثلاثة أحرف لاتينية (SYP أو USD…).",
                "• تاريخ السعر بصيغة 2026-10-05 ولا يكون في المستقبل. مصدر السعر رقم عرض أو وصف مستند: لا سعر بلا مصدر.",
                "• رمز المادة اختياري: إن وُجد جُمعت به أسعار المادة، وإلا جُمعت باسمها؛ مع الوحدة والعملة فلا تُقارَن أسعار بوحدتين أو عملتين.",
                "• السعر نفسه (المادة والوحدة والعملة والمورد والتاريخ والسعر) مرتين في الملف أو مسجَّلاً مسبقاً خطأ.",
                "• ارفع الملف أولاً للمعاينة: لا يُحفظ شيء حتى تؤكد، وإن وُجد خطأ واحد لا يُحفظ أي سطر (الكل أو لا شيء).",
                "• الأسعار المستوردة «تاريخية» وتنتظر الاعتماد من غير من استوردها.",
                "• الحد الأقصى 1000 سطر في الملف الواحد.",
            ];
            for (var i = 0; i < lines.Length; i++) help.Cell(i + 1, 1).Value = lines[i];
            help.Cell(1, 1).Style.Font.Bold = true;
            help.Column(1).Width = 110;
            return Save(workbook);
        }

        public List<ReferencePriceSheetRow> Read(Stream file, int maxRows)
        {
            XLWorkbook workbook;
            try { workbook = new XLWorkbook(file); }
            catch (Exception) { throw new InvalidOperationException("تعذّرت قراءة الملف — ارفع ملف Excel بصيغة ‎.xlsx (استخدم قالب الاستيراد)"); }

            using (workbook)
            {
                var sheet = workbook.Worksheets.FirstOrDefault(w => w.Name == SheetName) ?? workbook.Worksheet(1);
                var headerRow = sheet.FirstRowUsed() ?? throw new InvalidOperationException("الملف فارغ");

                // موقع كل عمود من عنوانه
                var positions = new Dictionary<int, int>();
                foreach (var cell in headerRow.CellsUsed())
                {
                    var title = cell.GetString().Trim().TrimEnd('*').Trim();
                    var index = Array.FindIndex(Columns, c => c.Header == title);
                    if (index >= 0) positions[index] = cell.Address.ColumnNumber;
                }
                var missing = Columns.Where((c, i) => c.Required && !positions.ContainsKey(i)).Select(c => c.Header).ToList();
                if (missing.Count > 0)
                    throw new InvalidOperationException("أعمدة مطلوبة غير موجودة في الملف: " + string.Join("، ", missing) + " — استخدم قالب الاستيراد");

                var rows = new List<ReferencePriceSheetRow>();
                foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
                {
                    var item = new ReferencePriceSheetRow { Row = row.RowNumber() };
                    foreach (var (index, column) in positions) Columns[index].Set(item, TextOf(row.Cell(column)));
                    // سطر المثال في القالب، والأسطر الفارغة
                    if (Columns.All(c => string.IsNullOrWhiteSpace(c.Get(item))) || item.Notes.Contains(ExampleMark)) continue;
                    rows.Add(item);
                    if (rows.Count > maxRows) throw new InvalidOperationException($"الملف يتجاوز {maxRows} سطر — قسّمه إلى أكثر من ملف");
                }
                return rows;
            }
        }

        /// <summary>نص الخلية كما كُتب: الأرقام بنقطة عشرية ثابتة (لا بتنسيق لغة الجهاز) والتواريخ yyyy-MM-dd</summary>
        private static string TextOf(IXLCell cell) => cell.DataType switch
        {
            XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Number => ((decimal)cell.GetDouble()).ToString(CultureInfo.InvariantCulture),
            _ => cell.GetFormattedString().Trim()
        };
    }
}
