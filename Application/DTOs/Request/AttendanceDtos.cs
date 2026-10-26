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

    /// <summary>قائمة الأشخاص: فلاتر اختيارية ضمن حد المستخدم. Missing: number / face / finger / templates</summary>
    public class AttendancePeopleSearchDto
    {
        public int? Kind { get; set; }
        public int? AreaId { get; set; }
        public int? BranchId { get; set; }
        public int? DepartmentId { get; set; }
        /// <summary>الاسم أو الرقم الذاتي أو رقم البصمة أو البطاقة</summary>
        public string? Query { get; set; }
        public string? Missing { get; set; }
        public bool? Active { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>بيانات حضور شخص: رقم البصمة (أرقام فقط، فريد)، البطاقة، مدير على الجهاز، مناطق إضافية، فترة العمل</summary>
    public class AttendanceProfileRequestDto
    {
        public string? AttendanceNumber { get; set; }
        public string? CardNumber { get; set; }
        public bool IsDeviceAdmin { get; set; }
        public List<int> ExtraAreaIds { get; set; } = [];
        public DateTime? ActiveFrom { get; set; }
        public DateTime? ActiveTo { get; set; }
        /// <summary>اربط البصمات التي وصلت بهذا الرقم ولا صاحب لها بهذا الشخص</summary>
        public bool LinkUnknownPunches { get; set; }
    }

    /// <summary>عامل مياومة: يتبع المنطقة التي يعمل فيها (إلزامية)، والفرع والقسم والمكتب اختيارية (الفرع داخل المنطقة)</summary>
    public class DailyWorkerRequestDto : AttendanceProfileRequestDto
    {
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? PersonalIdNumber { get; set; }
        public int? AreaId { get; set; }
        public int? BranchId { get; set; }
        public int? DepartmentId { get; set; }
        public int? OfficeId { get; set; }
        public bool IsActive { get; set; } = true;
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
