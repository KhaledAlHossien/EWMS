using Application.Interfaces;
using ClosedXML.Excel;

namespace Infrastructure.Files
{
    /// <summary>
    /// ملف استيراد الأشخاص للحضور (ClosedXML): قالب بأعمدة نصية وورقة تعليمات، وقراءة الأسطر بأسماء العناوين لا بترتيبها.
    /// </summary>
    public class AttendancePeopleSpreadsheet : IAttendancePeopleSpreadsheet
    {
        private const string SheetName = "الأشخاص";

        /// <summary>أسطر المثال في القالب (رمادية): تُتخطى عند القراءة إن بقيت في الملف</summary>
        private static readonly string[][] Examples =
        [
            ["موظف بلا دخول", "سامر أحمد الحسن (مثال)", "M201160", "1001", "", "", "الفرع التقني", "العمليات", "", "0991234567", "", ""],
            ["عامل مياومة", "خالد محمود (مثال)", "", "5001", "", "المنطقة الجنوبية", "", "", "", "", "2026-11-01", "2026-12-31"],
            ["تحديث", "(مثال)", "M201161", "1002", "", "", "", "", "", "", "", ""],
        ];

        private static readonly (string Header, Func<PeopleSheetRow, string> Get, Action<PeopleSheetRow, string> Set)[] Columns =
        [
            ("النوع", r => r.Kind, (r, v) => r.Kind = v),
            ("الاسم الكامل", r => r.FullName, (r, v) => r.FullName = v),
            ("الرقم الذاتي", r => r.PersonalIdNumber, (r, v) => r.PersonalIdNumber = v),
            ("رقم البصمة", r => r.AttendanceNumber, (r, v) => r.AttendanceNumber = v),
            ("رقم البطاقة", r => r.CardNumber, (r, v) => r.CardNumber = v),
            ("المنطقة", r => r.Area, (r, v) => r.Area = v),
            ("الفرع", r => r.Branch, (r, v) => r.Branch = v),
            ("القسم", r => r.Department, (r, v) => r.Department = v),
            ("المكتب", r => r.Office, (r, v) => r.Office = v),
            ("الهاتف", r => r.Phone, (r, v) => r.Phone = v),
            ("بداية العمل", r => r.ActiveFrom, (r, v) => r.ActiveFrom = v),
            ("نهاية العمل", r => r.ActiveTo, (r, v) => r.ActiveTo = v),
        ];

        public byte[] ImportTemplate()
        {
            using var workbook = new XLWorkbook { RightToLeft = true };
            var sheet = workbook.Worksheets.Add(SheetName);
            sheet.RightToLeft = true;
            for (var c = 0; c < Columns.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = Columns[c].Header;
                sheet.Column(c + 1).Style.NumberFormat.Format = "@";   // الأرقام نصوص: لا تضيع الأصفار ولا تتحوّل التواريخ
            }
            var header = sheet.Range(1, 1, 1, Columns.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");

            var examples = Examples;
            for (var r = 0; r < examples.Length; r++)
                for (var c = 0; c < examples[r].Length; c++) sheet.Cell(r + 2, c + 1).Value = examples[r][c];
            sheet.Range(2, 1, examples.Length + 1, Columns.Length).Style.Font.FontColor = XLColor.Gray;
            sheet.SheetView.FreezeRows(1);
            sheet.Columns().AdjustToContents(8, 40);

            var help = workbook.Worksheets.Add("تعليمات");
            help.RightToLeft = true;
            string[] lines =
            [
                "تعليمات استيراد الأشخاص للحضور",
                "• النوع: «موظف بلا دخول» أو «عامل مياومة» لإضافة شخص جديد، أو «تحديث» لإسناد رقم بصمة أو بطاقة لموظف موجود.",
                "• الموظف الموجود يُعرف برقمه الذاتي: سطره يحدّث رقم البصمة ورقم البطاقة فقط (ولو كُتب نوع آخر).",
                "• موظف بلا دخول: الاسم والرقم الذاتي مطلوبان، والفرع والقسم والمكتب بأسمائها كما في النظام.",
                "• عامل مياومة: الاسم والمنطقة مطلوبان؛ الرقم الذاتي والفرع اختياريان (الفرع يجب أن يتبع المنطقة).",
                "• رقم البصمة أرقام فقط حتى 9 خانات، فريد في المؤسسة كلها. التاريخ بصيغة 2026-11-01.",
                "• ارفع الملف أولاً للمعاينة: لا يُحفظ شيء حتى تؤكد، وتُحفظ الأسطر الصالحة فقط.",
                "• احذف أسطر المثال الرمادية. الحد الأقصى 2000 سطر في الملف الواحد.",
            ];
            for (var i = 0; i < lines.Length; i++) help.Cell(i + 1, 1).Value = lines[i];
            help.Cell(1, 1).Style.Font.Bold = true;
            help.Column(1).Width = 110;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public List<PeopleSheetRow> Read(Stream file, int maxRows)
        {
            XLWorkbook workbook;
            try { workbook = new XLWorkbook(file); }
            catch (Exception) { throw new InvalidOperationException("تعذّرت قراءة الملف — ارفع ملف Excel بصيغة ‎.xlsx (استخدم قالب الاستيراد)"); }

            using (workbook)
            {
                var sheet = workbook.Worksheets.FirstOrDefault(w => w.Name == SheetName) ?? workbook.Worksheet(1);
                var headerRow = sheet.FirstRowUsed() ?? throw new InvalidOperationException("الملف فارغ");
                var positions = new Dictionary<int, int>();
                foreach (var cell in headerRow.CellsUsed())
                {
                    var index = Array.FindIndex(Columns, c => c.Header == cell.GetString().Trim().TrimEnd('*').Trim());
                    if (index >= 0) positions[index] = cell.Address.ColumnNumber;
                }
                if (!positions.ContainsKey(1) || !positions.ContainsKey(3))
                    throw new InvalidOperationException("عمودا «الاسم الكامل» و«رقم البصمة» مطلوبان في الملف — استخدم قالب الاستيراد");

                var rows = new List<PeopleSheetRow>();
                foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
                {
                    var item = new PeopleSheetRow { Row = row.RowNumber() };
                    foreach (var (index, column) in positions)
                    {
                        var cell = row.Cell(column);
                        var text = cell.DataType == XLDataType.DateTime ? cell.GetDateTime().ToString("yyyy-MM-dd") : cell.GetFormattedString();
                        Columns[index].Set(item, text.Trim());
                    }
                    if (Columns.All(c => string.IsNullOrWhiteSpace(c.Get(item))) || item.FullName.EndsWith("(مثال)", StringComparison.Ordinal)) continue;
                    rows.Add(item);
                    if (rows.Count > maxRows) throw new InvalidOperationException($"الملف يتجاوز {maxRows} سطر — قسّمه إلى أكثر من ملف");
                }
                return rows;
            }
        }
    }
}
