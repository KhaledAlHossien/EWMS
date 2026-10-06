using Application.Interfaces;
using ClosedXML.Excel;

namespace Infrastructure.Files
{
    /// <summary>
    /// ملفات Excel لتوثيق الأجهزة (ClosedXML): تصدير التركيبات بلا كلمات السر، قالب الاستيراد، وقراءة ملف الاستيراد.
    /// الأعمدة تُقرأ بأسماء العناوين (لا بترتيبها)، فإضافة عمود أو تبديل الترتيب لا يكسر الملف.
    /// </summary>
    public class DeviceSpreadsheet : IDeviceSpreadsheet
    {
        // عنوان العمود ← الحقل (القالب يكتبها بهذا الترتيب؛ * = مطلوب)
        private static readonly (string Header, Func<InstallationSheetRow, string> Get, Action<InstallationSheetRow, string> Set, bool Required)[] Columns =
        [
            ("الموقع", r => r.Site, (r, v) => r.Site = v, true),
            ("الجهاز", r => r.Device, (r, v) => r.Device = v, true),
            ("الموديل", r => r.Model, (r, v) => r.Model = v, false),
            ("الفئة", r => r.Category, (r, v) => r.Category = v, false),
            ("الشركة المصنعة", r => r.Manufacturer, (r, v) => r.Manufacturer = v, false),
            ("الرقم التسلسلي", r => r.SN, (r, v) => r.SN = v, false),
            ("مكان التركيب", r => r.InstallLocation, (r, v) => r.InstallLocation = v, false),
            ("IP", r => r.Ip, (r, v) => r.Ip = v, true),
            ("قناع الشبكة", r => r.SubnetMask, (r, v) => r.SubnetMask = v, true),
            ("البوابة", r => r.Gateway, (r, v) => r.Gateway = v, false),
            ("MAC", r => r.MacAddress, (r, v) => r.MacAddress = v, false),
            ("المنفذ", r => r.Port, (r, v) => r.Port = v, false),
            ("VLAN", r => r.Vlan, (r, v) => r.Vlan = v, false),
            ("المستخدم", r => r.UserName, (r, v) => r.UserName = v, true),
            ("كلمة السر", r => r.Pass, (r, v) => r.Pass = v, true),
            ("البرنامج الثابت", r => r.Firmware, (r, v) => r.Firmware = v, false),
            ("تاريخ التركيب", r => r.InstallDate, (r, v) => r.InstallDate = v, false),
            ("الحالة", r => r.Status, (r, v) => r.Status = v, false),
            ("ملاحظات", r => r.Note, (r, v) => r.Note = v, false),
        ];

        private const string SheetName = "التركيبات";

        private static void StyleHeader(IXLRange header)
        {
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");
            header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        private static byte[] Save(XLWorkbook workbook)
        {
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] ExportInstallations(IEnumerable<InstallationRow> rows)
        {
            using var workbook = new XLWorkbook { RightToLeft = true };
            var sheet = workbook.Worksheets.Add(SheetName);
            sheet.RightToLeft = true;

            string[] headers = ["الموقع", "المحافظة", "الجهاز", "الموديل", "الفئة", "الرقم التسلسلي", "مكان التركيب", "IP", "قناع الشبكة", "البوابة",
                "MAC", "المنفذ", "VLAN", "المستخدم", "البرنامج الثابت", "تاريخ التركيب", "آخر تحقق", "الحالة", "ملاحظات"];
            for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
            StyleHeader(sheet.Range(1, 1, 1, headers.Length));

            var r = 2;
            foreach (var row in rows)
            {
                var i = row.Installation;
                object?[] values =
                [
                    i.Site?.Name, Application.Common.Governorates.NameOf(i.Site?.GovernorateCode ?? ""), i.Device?.Name, i.Device?.Model, i.Device?.Category,
                    i.SN, i.InstallLocation, i.Ip, i.SubnetMask, i.Gateway, i.MacAddress, i.Port, i.Vlan,
                    i.UserName, i.Firmware, i.InstallDate?.ToString("yyyy-MM-dd"), i.LastVerifiedAt?.ToString("yyyy-MM-dd"),
                    Application.Features.DeviceInventory.DeviceInventoryRules.StatusAr(i.Status), i.Note
                ];
                for (var c = 0; c < values.Length; c++)
                {
                    var cell = sheet.Cell(r, c + 1);
                    cell.Value = values[c] switch { null => (XLCellValue)Blank.Value, int n => (XLCellValue)n, var v => (XLCellValue)(v.ToString() ?? "") };
                    if (values[c] is string) cell.Style.NumberFormat.Format = "@";   // الـ IP والأرقام نصوص (لا تتحوّل إلى أرقام)
                }
                r++;
            }

            sheet.SheetView.FreezeRows(1);
            sheet.Columns().AdjustToContents(1, Math.Min(r, 200), 8, 50);
            return Save(workbook);
        }

        public byte[] ImportTemplate()
        {
            using var workbook = new XLWorkbook { RightToLeft = true };
            var sheet = workbook.Worksheets.Add(SheetName);
            sheet.RightToLeft = true;
            for (var c = 0; c < Columns.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = Columns[c].Header;
                sheet.Column(c + 1).Style.NumberFormat.Format = "@";   // كل الأعمدة نصية: الـ IP والتاريخ يُكتبان كما هما
            }
            StyleHeader(sheet.Range(1, 1, 1, Columns.Length));
            for (var c = 0; c < Columns.Length; c++)
                if (Columns[c].Required) sheet.Cell(1, c + 1).Style.Font.FontColor = XLColor.FromHtml("#B42318");

            string[] example = ["اسم موقع موجود", "كاميرا مراقبة", "DS-2CD1043", "كاميرا", "Hikvision", "SN123456", "عند البوابة الرئيسية",
                "192.168.1.10", "255.255.255.0", "192.168.1.1", "AA:BB:CC:DD:EE:FF", "Gi0/12", "10", "admin", "كلمة السر", "V5.7.3", "2026-10-05", "يعمل", "مثال — احذف هذا السطر"];
            for (var c = 0; c < example.Length; c++) sheet.Cell(2, c + 1).Value = example[c];
            sheet.Range(2, 1, 2, Columns.Length).Style.Font.FontColor = XLColor.Gray;
            sheet.SheetView.FreezeRows(1);
            sheet.Columns().AdjustToContents(8, 40);

            var help = workbook.Worksheets.Add("تعليمات");
            help.RightToLeft = true;
            string[] lines =
            [
                "تعليمات استيراد التركيبات",
                "• الأعمدة الحمراء مطلوبة: الموقع، الجهاز، IP، قناع الشبكة، المستخدم، كلمة السر.",
                "• الموقع يجب أن يكون مضافاً مسبقاً بنفس الاسم تماماً (يحتاج إحداثيات على الخريطة) — وإلا يُرفض السطر.",
                "• الجهاز (الاسم + الموديل) يُضاف إلى الكتالوج تلقائياً إن لم يكن موجوداً.",
                "• قناع الشبكة صحيح مثل 255.255.255.0، والبوابة يجب أن تكون في شبكة الجهاز.",
                "• الحالة: يعمل أو معطّل أو أُزيل (الفارغ = يعمل). التاريخ بصيغة 2026-10-05.",
                "• تكرار الـ IP في نفس الموقع مسموح وينبَّه عليه فقط.",
                "• ارفع الملف أولاً للمعاينة: لا يُحفظ شيء حتى تؤكد، وتُحفظ الأسطر الصالحة فقط.",
                "• الحد الأقصى 2000 سطر في الملف الواحد.",
            ];
            for (var i = 0; i < lines.Length; i++) help.Cell(i + 1, 1).Value = lines[i];
            help.Cell(1, 1).Style.Font.Bold = true;
            help.Column(1).Width = 110;
            return Save(workbook);
        }

        public List<InstallationSheetRow> ReadInstallations(Stream file, int maxRows)
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

                var rows = new List<InstallationSheetRow>();
                foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
                {
                    var item = new InstallationSheetRow { Row = row.RowNumber() };
                    foreach (var (index, column) in positions)
                    {
                        var cell = row.Cell(column);
                        var text = cell.DataType == XLDataType.DateTime ? cell.GetDateTime().ToString("yyyy-MM-dd") : cell.GetFormattedString();
                        Columns[index].Set(item, text.Trim());
                    }
                    // سطر المثال في القالب، والأسطر الفارغة
                    if (Columns.All(c => string.IsNullOrWhiteSpace(c.Get(item))) || item.Note.Contains("مثال — احذف هذا السطر")) continue;
                    rows.Add(item);
                    if (rows.Count > maxRows)
                        throw new InvalidOperationException($"الملف يتجاوز {maxRows} سطر — قسّمه إلى أكثر من ملف");
                }
                return rows;
            }
        }
    }
}
