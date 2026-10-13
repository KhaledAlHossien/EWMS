namespace Application.DTOs.Response
{
    public class PublicHolidayResponseDto
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Days { get; set; }        // أيام العطلة (من–إلى شاملة)
        public int Fridays { get; set; }     // منها أيام جمعة أصلاً (لا تغيّر شيئاً في الحساب)
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
