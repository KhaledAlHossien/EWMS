namespace Application.DTOs.Request
{
    /// <summary>
    /// عطلة رسمية بفترة: StartDate وEndDate (اختياري، الفارغ = يوم واحد) — سجل واحد للعطلة كلها (مثل عطلة العيد)،
    /// والتعديل يغيّر الاسم والفترة معاً.
    /// </summary>
    public class PublicHolidayRequestDto
    {
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
