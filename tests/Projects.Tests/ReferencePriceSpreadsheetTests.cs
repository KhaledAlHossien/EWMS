using ClosedXML.Excel;
using Infrastructure.Files;

namespace Projects.Tests;

/// <summary>ملف Excel للأسعار المرجعية: القالب يُقرأ بلا أسطر (المثال يُتخطى)، والأعمدة تُقرأ بعناوينها، والأرقام والتواريخ بصيغ ثابتة.</summary>
public class ReferencePriceSpreadsheetTests
{
    private static readonly ReferencePriceSpreadsheet Sheet = new();

    private static MemoryStream Workbook(string[] headers, params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("الأسعار");
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
            {
                ws.Cell(r + 2, c + 1).Value = rows[r][c] switch
                {
                    null => Blank.Value,
                    DateTime d => d,
                    double n => n,
                    int n => n,
                    var v => v.ToString()!
                };
            }
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static readonly string[] Required = ["اسم المادة", "الوحدة", "المورد", "سعر الوحدة", "العملة", "تاريخ السعر", "مصدر السعر"];

    [Fact]
    public void The_template_reads_back_as_no_rows_because_the_example_line_is_skipped()
    {
        using var stream = new MemoryStream(Sheet.Template());
        Assert.Empty(Sheet.Read(stream, 100));
    }

    [Fact]
    public void Columns_are_found_by_their_headers_in_any_order_and_cells_are_read_as_stable_text()
    {
        string[] headers = ["مصدر السعر", "تاريخ السعر", "العملة", "سعر الوحدة", "المورد", "الوحدة", "اسم المادة", "رمز المادة"];
        using var stream = Workbook(headers, [["عرض 7", new DateTime(2026, 10, 5), "SYP", 1250.5, "مورد أ", "م", "سلك", "CU-25"]]);

        var row = Assert.Single(Sheet.Read(stream, 100));

        Assert.Equal(2, row.Row);
        Assert.Equal("سلك", row.ItemName);
        Assert.Equal("CU-25", row.ItemCode);
        Assert.Equal("1250.5", row.UnitPrice);          // نقطة عشرية ثابتة بغض النظر عن لغة الجهاز
        Assert.Equal("2026-10-05", row.PriceDate);      // خلية تاريخ ← yyyy-MM-dd
        Assert.Equal("عرض 7", row.SourceDocument);
    }

    [Fact]
    public void Blank_rows_are_skipped_and_a_missing_required_column_names_itself()
    {
        using var ok = Workbook(Required, [["سلك", "م", "مورد أ", "10", "SYP", "2026-10-05", "عرض"], [null, null, null, null, null, null, null]]);
        Assert.Single(Sheet.Read(ok, 100));

        using var missing = Workbook(["اسم المادة", "الوحدة"], ["سلك", "م"]);
        var error = Assert.Throws<InvalidOperationException>(() => Sheet.Read(missing, 100));
        Assert.Contains("المورد", error.Message);
        Assert.Contains("مصدر السعر", error.Message);
    }

    [Fact]
    public void More_rows_than_the_limit_are_refused_and_a_non_excel_file_gets_a_clear_message()
    {
        using var big = Workbook(Required, Enumerable.Range(0, 5).Select(i => new object[] { $"م{i}", "م", "مورد أ", "10", "SYP", "2026-10-05", "عرض" }).ToArray());
        Assert.Throws<InvalidOperationException>(() => Sheet.Read(big, 3));

        using var junk = new MemoryStream("هذا ليس ملف إكسل"u8.ToArray());
        Assert.Contains("xlsx", Assert.Throws<InvalidOperationException>(() => Sheet.Read(junk, 10)).Message);
    }
}
