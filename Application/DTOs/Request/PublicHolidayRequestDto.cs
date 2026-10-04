namespace Application.DTOs.Request
{
    /// <summary>
    /// عطلة رسمية. عند الإضافة يمكن تحديد EndDate لإضافة عدة أيام دفعة واحدة (مثل عطلة العيد) — يومٌ لكل سجل.
    /// عند التعديل يُتجاهل EndDate.
    /// </summary>
    public class PublicHolidayRequestDto
    {
        public DateTime Date { get; set; }
        public DateTime? EndDate { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
