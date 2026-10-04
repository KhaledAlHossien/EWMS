namespace Domain.Entities
{
    /// <summary>
    /// جزء من إجازة معتمدة بحالة دفع واحدة داخل شهر واحد — يُنشأ عند الاعتماد النهائي فقط
    /// (قرار المستخدم 2026-10-04: الحد الشهري يُحسب من المعتمد فقط ويُحدَّد الدفع عند الاعتماد).
    /// StartDate/EndDate أول وآخر يوم عمل في الجزء، وDays عدد أيام العمل فيه (بلا جمعة ولا عطل رسمية).
    /// </summary>
    public class VacationSegment
    {
        public int Id { get; set; }

        public int VacationId { get; set; }
        public Vacation Vacation { get; set; } = null!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsPaid { get; set; }
        public int Days { get; set; }
    }
}
