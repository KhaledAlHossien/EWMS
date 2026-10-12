namespace Domain.Entities
{
    public enum ReportFormat
    {
        Excel = 1,
        Print = 2
    }

    /// <summary>
    /// سجل سحب التقارير (مركز التقارير، 2026-10-12): من سحب أي تقرير، بأي فلاتر، وكم صفاً، ومتى.
    /// المعاينة لا تُسجَّل. المستخدم Restrict (حماية حذف المستخدم: يُعطَّل بدلاً من الحذف).
    /// BranchId = فرع المستخدم لحظة السحب، لحدّ صفحة السجل (بلا مفتاح أجنبي: يبقى الأثر ولو حُذف الفرع).
    /// </summary>
    public class ReportExportLog
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int? BranchId { get; set; }

        public string ReportKey { get; set; } = string.Empty;
        public string ReportTitle { get; set; } = string.Empty;
        public ReportFormat Format { get; set; }
        public string Filters { get; set; } = string.Empty;   // نص عربي مقروء: «القسم: الصيانة · من تاريخ: …»
        public int RowCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
