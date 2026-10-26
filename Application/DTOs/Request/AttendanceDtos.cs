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

    /// <summary>قبول جهاز أو تعديله: اسمه ومنطقته (إلزامية للجهاز المقبول) وملاحظاته</summary>
    public class AttendanceDeviceRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public int? AreaId { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// أمر يدوي لجهاز: SyncNow (مزامنة الأشخاص الآن)، FullResync (إعادة إرسال الكل)، QueryPunches (سحب السجلات من From إلى To)،
    /// QueryTemplates (سحب القوالب)، QueryUsers (سحب قائمة المستخدمين)، RefreshInfo، SetTime، Reboot
    /// </summary>
    public class AttendanceDeviceCommandRequestDto
    {
        public string Kind { get; set; } = string.Empty;
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    /// <summary>بحث البصمات الخام: الفترة (الافتراضي اليوم) وفلاتر اختيارية — كلها ضمن حدّ صلاحيات العرض</summary>
    public class AttendancePunchSearchDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? AreaId { get; set; }
        public int? DeviceId { get; set; }
        public int? UserId { get; set; }
        /// <summary>اسم الشخص أو رقمه الذاتي أو رقم البصمة</summary>
        public string? Query { get; set; }
        /// <summary>البصمات بأرقام لا صاحب لها فقط</summary>
        public bool UnknownOnly { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
