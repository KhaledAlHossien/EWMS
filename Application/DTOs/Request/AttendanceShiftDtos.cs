namespace Application.DTOs.Request
{
    // الحضور — المرحلة الثانية: الجداول الزمنية والورديات والجدولة والتصحيحات. الأوقات بالدقائق من منتصف الليل.

    public class AttendanceTimetableRequestDto
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>1 ثابت، 2 مرن</summary>
        public int Kind { get; set; }
        public string Color { get; set; } = "#2E7D32";
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }
        public int CheckInBeforeMinutes { get; set; }
        public int CheckInAfterMinutes { get; set; }
        public int CheckOutBeforeMinutes { get; set; }
        public int CheckOutAfterMinutes { get; set; }
        public int LateGraceMinutes { get; set; }
        public int EarlyLeaveGraceMinutes { get; set; }
        /// <summary>1 من وقت البدء، 2 بعد السماح</summary>
        public int LateCounting { get; set; } = 1;
        public bool RequireCheckIn { get; set; } = true;
        public bool RequireCheckOut { get; set; } = true;
        public int BreakMinutes { get; set; }
        public int RequiredMinutes { get; set; }
        public bool IsActive { get; set; } = true;
        public byte[]? RowVersion { get; set; }
    }

    public class AttendanceShiftDayRequestDto
    {
        public int DayIndex { get; set; }
        public List<int> TimetableIds { get; set; } = [];
    }

    public class AttendanceShiftRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>1 أسابيع (تبدأ من السبت)، 2 أيام</summary>
        public int CycleUnit { get; set; } = 1;
        public int CycleLength { get; set; } = 1;
        public DateTime StartDate { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>الأيام التي فيها دوام فقط؛ ما لا يُذكر يوم راحة</summary>
        public List<AttendanceShiftDayRequestDto> Days { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>إسناد وردية: TargetType 1 منطقة، 2 فرع، 3 قسم، 4 مكتب، 5 شخص</summary>
    public class AttendanceScheduleRequestDto
    {
        public int TargetType { get; set; }
        public int TargetId { get; set; }
        public int ShiftId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>جدولة مؤقتة: جدول بديل أو راحة (TimetableId فارغ) لأيام محددة</summary>
    public class AttendanceTemporaryRequestDto
    {
        public int TargetType { get; set; }
        public int TargetId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? TimetableId { get; set; }
        public string? Note { get; set; }
    }
}
