namespace Application.DTOs.Response
{
    public class PublicHolidayResponseDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsFriday { get; set; }   // يقع يوم جمعة أصلاً (لا يغيّر شيئاً في الحساب)
    }

    /// <summary>معاينة مدة الإجازة في نموذج الطلب</summary>
    public class VacationDaysPreviewDto
    {
        public int CalendarDays { get; set; }
        public int WorkingDays { get; set; }
        public int Fridays { get; set; }
        public List<PublicHolidayResponseDto> Holidays { get; set; } = new();
    }
}
