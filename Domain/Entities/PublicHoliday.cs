namespace Domain.Entities
{
    /// <summary>
    /// عطلة رسمية — يوم لا يُحسب من مدة الإجازة (مع الجمعة، العطلة الأسبوعية). قرار المستخدم 2026-10-04.
    /// </summary>
    public class PublicHoliday
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }          // تاريخ فقط (عمود date)، فريد
        public string Name { get; set; } = string.Empty;
    }
}
