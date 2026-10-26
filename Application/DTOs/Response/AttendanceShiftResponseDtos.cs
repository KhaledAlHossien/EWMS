namespace Application.DTOs.Response
{
    public class AttendanceTimetableDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Kind { get; set; }
        public string KindName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }
        /// <summary>يعبر منتصف الليل</summary>
        public bool Overnight { get; set; }
        public int SpanMinutes { get; set; }
        public int CheckInBeforeMinutes { get; set; }
        public int CheckInAfterMinutes { get; set; }
        public int CheckOutBeforeMinutes { get; set; }
        public int CheckOutAfterMinutes { get; set; }
        public int LateGraceMinutes { get; set; }
        public int EarlyLeaveGraceMinutes { get; set; }
        public int LateCounting { get; set; }
        public bool RequireCheckIn { get; set; }
        public bool RequireCheckOut { get; set; }
        public int BreakMinutes { get; set; }
        public int RequiredMinutes { get; set; }
        public bool IsActive { get; set; }
        /// <summary>عدد الورديات التي تستعمله</summary>
        public int UsedByShifts { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>جدول مختصر داخل يوم الوردية</summary>
    public class AttendanceTimetableBriefDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Kind { get; set; }
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }
        public int RequiredMinutes { get; set; }
    }

    public class AttendanceShiftDayDto
    {
        public int DayIndex { get; set; }
        public List<AttendanceTimetableBriefDto> Timetables { get; set; } = [];
    }

    public class AttendanceShiftDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CycleUnit { get; set; }
        public int CycleLength { get; set; }
        public int CycleDays { get; set; }
        public DateTime StartDate { get; set; }
        public bool IsActive { get; set; }
        /// <summary>أيام الدوام في الدورة (والباقي راحة)</summary>
        public List<AttendanceShiftDayDto> Days { get; set; } = [];
        /// <summary>عدد الإسنادات التي تستعملها</summary>
        public int UsedBySchedules { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
