namespace Domain.Entities
{
    public class VacationType
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// هل النوع مدفوع افتراضياً؟
        /// true → يُحتسب من الحد الشهري (2 يوم مدفوع)
        /// false → غير مدفوع دائماً (يُخصم من الراتب)
        /// </summary>
        public bool IsPaid { get; set; } = true;
    }
}