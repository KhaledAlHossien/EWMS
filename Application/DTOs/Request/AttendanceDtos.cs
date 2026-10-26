namespace Application.DTOs.Request
{
    // الحضور والانصراف — RowVersion يُعاد كما وصل في الاستجابة عند التعديل (تعديل متزامن = رسالة واضحة)

    public class AttendanceAreaRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>فروع المنطقة بالضبط: المختار يُنقل إليها، والمحذوف من القائمة يصبح بلا منطقة</summary>
        public List<int> BranchIds { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }
}
