using Application.DTOs.Response;
using Application.Interfaces;
using ClosedXML.Excel;

namespace Infrastructure.Files
{
    /// <summary>
    /// كاتب Excel الموحّد لمركز التقارير (ClosedXML): ورقة من اليمين، ترويسة (العنوان، الفلاتر، من سحب ومتى، عدد الصفوف)،
    /// صف عناوين مثبّت مع فلترة، تنسيق حسب نوع العمود، وسطر «المجموع» للأعمدة المجموعة. يكفي لكل التقارير.
    /// </summary>
    public class ReportSpreadsheet : IReportSpreadsheet
    {
        private const int HeaderRow = 5;

        public byte[] Build(ReportSheet sheet)
        {
            var columns = sheet.Table.Columns;
            var rows = sheet.Table.Rows;
            var width = Math.Max(columns.Count, 1);

            using var workbook = new XLWorkbook { RightToLeft = true };
            var ws = workbook.Worksheets.Add(SheetName(sheet.Title));
            ws.RightToLeft = true;

            Info(ws, 1, width, sheet.Title).Style.Font.SetBold().Font.SetFontSize(14);
            Info(ws, 2, width, "الفلاتر: " + sheet.FiltersText);
            Info(ws, 3, width, $"سحبه: {sheet.ExportedBy} — {sheet.ExportedAt:yyyy-MM-dd HH:mm} — عدد الصفوف: {rows.Count:N0}");

            for (var c = 0; c < columns.Count; c++) ws.Cell(HeaderRow, c + 1).Value = columns[c].Header;
            var header = ws.Range(HeaderRow, 1, HeaderRow, width);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");
            header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            header.Style.Alignment.WrapText = true;

            var r = HeaderRow + 1;
            foreach (var row in rows)
            {
                for (var c = 0; c < columns.Count && c < row.Length; c++)
                    ws.Cell(r, c + 1).Value = Value(row[c]);
                r++;
            }
            var lastRow = Math.Max(r - 1, HeaderRow);

            // التنسيق لكل عمود دفعة واحدة (أسرع من خلية خلية)
            if (rows.Count > 0)
                for (var c = 0; c < columns.Count; c++)
                    ws.Range(HeaderRow + 1, c + 1, lastRow, c + 1).Style.NumberFormat.Format = Format(columns[c].Type);

            if (columns.Any(c => c.Sum) && rows.Count > 0)
            {
                var total = lastRow + 1;
                ws.Cell(total, 1).Value = "المجموع";
                for (var c = 0; c < columns.Count; c++)
                {
                    if (!columns[c].Sum) continue;
                    ws.Cell(total, c + 1).Value = rows.Sum(x => c < x.Length ? Number(x[c]) : 0m);
                    ws.Cell(total, c + 1).Style.NumberFormat.Format = Format(columns[c].Type);
                }
                var line = ws.Range(total, 1, total, width);
                line.Style.Font.Bold = true;
                line.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            ws.SheetView.FreezeRows(HeaderRow);
            ws.Range(HeaderRow, 1, lastRow, width).SetAutoFilter();

            // عرض الأعمدة من العناوين وأول 300 صف، بين 8 و60
            ws.Columns(1, width).AdjustToContents(HeaderRow, Math.Min(lastRow, HeaderRow + 300));
            foreach (var column in ws.Columns(1, width))
                column.Width = Math.Clamp(column.Width, 8, 60);

            ws.PageSetup.PageOrientation = width > 8 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static IXLRange Info(IXLWorksheet ws, int row, int width, string text)
        {
            ws.Cell(row, 1).Value = text;
            var range = ws.Range(row, 1, row, width).Merge();
            range.Style.Alignment.WrapText = true;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            return range;
        }

        /// <summary>اسم الورقة: 31 حرفاً بلا الرموز الممنوعة في Excel</summary>
        private static string SheetName(string title)
        {
            var clean = new string(title.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
            if (clean.Length == 0) clean = "تقرير";
            return clean.Length > 31 ? clean[..31] : clean;
        }

        private static XLCellValue Value(object? value) => value switch
        {
            null => Blank.Value,
            string s => s,
            int n => n,
            long l => l,
            decimal d => d,
            double x => x,
            DateTime t => t,
            bool b => b ? "نعم" : "لا",
            _ => value.ToString() ?? string.Empty
        };

        private static decimal Number(object? value) => value switch
        {
            int n => n,
            long l => l,
            decimal d => d,
            double x => (decimal)x,
            _ => 0m
        };

        private static string Format(string type) => type switch
        {
            ReportColumnType.Number => "#,##0",
            ReportColumnType.Money => "#,##0",
            ReportColumnType.Decimal => "#,##0.##",
            ReportColumnType.Date => "yyyy-mm-dd",
            ReportColumnType.DateTime => "yyyy-mm-dd hh:mm",
            _ => "@"
        };
    }
}
