using Application.DTOs.Response;
using Application.Interfaces;
using ClosedXML.Excel;

namespace Infrastructure.Files
{
    /// <summary>تصدير مهام لوحة المهام إلى Excel (ClosedXML): ورقة واحدة RTL، العنوان مثبّت، والمتأخرة بخط أحمر</summary>
    public class AssignedTaskSpreadsheet : IAssignedTaskSpreadsheet
    {
        private static string Date(DateTime? d) => d?.ToString("yyyy-MM-dd") ?? string.Empty;

        public byte[] Export(IEnumerable<AssignedTaskCardDto> tasks)
        {
            using var workbook = new XLWorkbook { RightToLeft = true };
            var sheet = workbook.Worksheets.Add("المهام");
            sheet.RightToLeft = true;

            string[] headers = ["#", "العنوان", "الحالة", "الأولوية", "الجهة المنفِّذة", "المسار", "المُسنِد", "تاريخ الإسناد", "تاريخ التسليم",
                "متأخرة", "تاريخ الإنجاز", "بنود التحقق", "المرفقات", "التعليقات", "المهام الفرعية", "يعمل عليها", "مهمة أصل"];
            for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
            var header = sheet.Range(1, 1, 1, headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0EA");
            header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            var r = 2;
            foreach (var t in tasks)
            {
                string[] values =
                [
                    t.Id.ToString(), t.Title, t.StatusAr, t.PriorityAr, t.TargetName, t.TargetPath, t.CreatedByName,
                    Date(t.CreatedAt), Date(t.DueDate), t.IsOverdue ? "نعم" : "", Date(t.CompletedAt),
                    t.ChecklistTotal == 0 ? "" : $"{t.ChecklistDone}/{t.ChecklistTotal}",
                    t.AttachmentsCount == 0 ? "" : t.AttachmentsCount.ToString(),
                    t.CommentsCount == 0 ? "" : t.CommentsCount.ToString(),
                    t.SubTasksTotal == 0 ? "" : $"{t.SubTasksDone}/{t.SubTasksTotal}",
                    t.ClaimedByName ?? "", t.ParentTitle ?? ""
                ];
                for (var c = 0; c < values.Length; c++)
                {
                    var cell = sheet.Cell(r, c + 1);
                    cell.Value = values[c];
                    cell.Style.NumberFormat.Format = "@";
                }
                if (t.IsOverdue) sheet.Range(r, 1, r, headers.Length).Style.Font.FontColor = XLColor.FromHtml("#B42318");
                r++;
            }

            sheet.SheetView.FreezeRows(1);
            sheet.Columns().AdjustToContents(1, Math.Min(r, 200), 8, 50);
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
