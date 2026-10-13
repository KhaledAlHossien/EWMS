namespace Domain.Entities
{
    /// <summary>
    /// عطلة رسمية — أيامها لا تُحسب من مدة الإجازة (مع الجمعة، العطلة الأسبوعية). قرار المستخدم 2026-10-04.
    /// سجل واحد لكل عطلة بفترة من–إلى (قرار المستخدم 2026-10-13: عيد من أربعة أيام بند واحد يُعدَّل ويُحذف كاملاً).
    /// العطل لا تتداخل؛ وحساب الإجازات يقرأ أيامها داخل المدة (IPublicHolidayService.GetDatesAsync).
    /// </summary>
    public class PublicHoliday
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }     // تاريخ فقط (عمود date)
        public DateTime EndDate { get; set; }       // = StartDate لعطلة يوم واحد
        public string Name { get; set; } = string.Empty;
    }
}
