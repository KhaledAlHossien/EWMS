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

    /// <summary>جهة إسناد (منطقة، فرع، قسم، مكتب، شخص) مع ما يميّزها</summary>
    public class ScheduleTargetDto
    {
        public int Type { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>الفرع للقسم، والقسم للمكتب، ورقم البصمة والقسم للشخص…</summary>
        public string Detail { get; set; } = string.Empty;
        /// <summary>جهة محذوفة: الإسناد لا ينطبق على أحد</summary>
        public bool Missing { get; set; }
    }

    public class AttendanceScheduleDto
    {
        public int Id { get; set; }
        public ScheduleTargetDto Target { get; set; } = new();
        public int ShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public bool ShiftActive { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Note { get; set; } = string.Empty;
        /// <summary>ساري اليوم</summary>
        public bool IsCurrent { get; set; }
        public bool IsPast { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? UpdatedByName { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class AttendanceTemporaryDto
    {
        public int Id { get; set; }
        public ScheduleTargetDto Target { get; set; } = new();
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        /// <summary>فارغ = راحة</summary>
        public AttendanceTimetableBriefDto? Timetable { get; set; }
        public string Note { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>الجدول الفعلي لشخص في يوم ومصدره</summary>
    public class EffectiveDayDto
    {
        public DateTime Date { get; set; }
        /// <summary>work / rest / unscheduled / inactive</summary>
        public string Kind { get; set; } = string.Empty;
        public string? ShiftName { get; set; }
        /// <summary>«شخص» / «فرع: …» … و«مؤقتة» إن جاءت من جدولة مؤقتة</summary>
        public string? Source { get; set; }
        public bool Temporary { get; set; }
        public List<AttendanceTimetableBriefDto> Timetables { get; set; } = [];
        public string? HolidayName { get; set; }
        public bool OnVacation { get; set; }
    }

    public class AttendanceEffectiveDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public List<EffectiveDayDto> Days { get; set; } = [];
    }

    /// <summary>شخص مراقَب (له رقم بصمة وفعّال) بلا وردية في اليوم المختار</summary>
    public class UnscheduledPersonDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string KindName { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string? AreaName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
    }

    public class AttendanceDayPartDto
    {
        public string TimetableName { get; set; } = string.Empty;
        public int Kind { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int LateMinutes { get; set; }
        public int EarlyMinutes { get; set; }
        public int ShortMinutes { get; set; }
        public int WorkMinutes { get; set; }
        public int RequiredMinutes { get; set; }
    }

    /// <summary>سطر في «الحضور اليومي»</summary>
    public class AttendanceDailyRowDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string KindName { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public string? ShiftName { get; set; }
        public int Type { get; set; }
        /// <summary>1 جارٍ، 2 حاضر، 3 غائب، 4 بصمة ناقصة، 5 مبرَّر، 6 راحة، 7 عطلة، 8 إجازة</summary>
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime? FirstIn { get; set; }
        public DateTime? LastOut { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyMinutes { get; set; }
        public int ShortMinutes { get; set; }
        public int WorkMinutes { get; set; }
        public int RequiredMinutes { get; set; }
        public bool HasExcuse { get; set; }
        public List<AttendanceDayPartDto> Parts { get; set; } = [];
    }

    public class AttendanceDailyCountsDto
    {
        public int Total { get; set; }
        public int Present { get; set; }
        public int Late { get; set; }
        public int Early { get; set; }
        public int Absent { get; set; }
        public int Incomplete { get; set; }
        public int Excused { get; set; }
        public int Pending { get; set; }
        public int Vacation { get; set; }
        public int Holiday { get; set; }
        public int Rest { get; set; }
    }

    public class AttendanceDailyDto
    {
        public DateTime Date { get; set; }
        public AttendanceDailyCountsDto Counts { get; set; } = new();
        public PagedResultDto<AttendanceDailyRowDto> Rows { get; set; } = new();
        /// <summary>يملك التصحيح (منطقتي أو الكل)</summary>
        public bool CanCorrect { get; set; }
    }

    public class SheetPunchDto
    {
        public long? PunchId { get; set; }
        public long? CorrectionId { get; set; }
        public DateTime Time { get; set; }
        public string? DeviceName { get; set; }
        public string Verify { get; set; } = string.Empty;
        public string VerifyName { get; set; } = string.Empty;
        /// <summary>بصمة يدوية من تصحيح</summary>
        public bool Manual { get; set; }
        /// <summary>متجاهلة بتصحيح (تبقى في السجل الخام)</summary>
        public bool Ignored { get; set; }
    }

    public class AttendanceSheetDayDto
    {
        public DateTime Date { get; set; }
        /// <summary>false = لا نتيجة: غير مجدول، أو خارج فترة العمل، أو لم يأتِ بعد</summary>
        public bool Calculated { get; set; }
        public bool Future { get; set; }
        public int? Type { get; set; }
        public int? Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? ShiftName { get; set; }
        public DateTime? FirstIn { get; set; }
        public DateTime? LastOut { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyMinutes { get; set; }
        public int ShortMinutes { get; set; }
        public int WorkMinutes { get; set; }
        public int RequiredMinutes { get; set; }
        public bool HasExcuse { get; set; }
        public List<AttendanceDayPartDto> Parts { get; set; } = [];
        public List<SheetPunchDto> Punches { get; set; } = [];
    }

    public class AttendanceSheetTotalsDto
    {
        public int WorkDays { get; set; }
        public int Present { get; set; }
        public int LateDays { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyMinutes { get; set; }
        public int ShortMinutes { get; set; }
        public int Absent { get; set; }
        public int Incomplete { get; set; }
        public int Excused { get; set; }
        public int Vacation { get; set; }
        public int Holiday { get; set; }
        public int Rest { get; set; }
        public int WorkMinutes { get; set; }
        public int RequiredMinutes { get; set; }
    }

    /// <summary>كشف شهر لشخص يوماً بيوم</summary>
    public class AttendanceSheetDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string KindName { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public string Month { get; set; } = string.Empty;
        public List<AttendanceSheetDayDto> Days { get; set; } = [];
        public AttendanceSheetTotalsDto Totals { get; set; } = new();
        public bool CanCorrect { get; set; }
    }

    public class AttendanceCorrectionDto
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public int Kind { get; set; }
        public string KindName { get; set; } = string.Empty;
        /// <summary>«بصمة يدوية 08:05» / «تجاهل بصمة 07:12» / «إذن 08:00 – 10:00» …</summary>
        public string Summary { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime? PunchTime { get; set; }
        public long? PunchId { get; set; }
        public int? ExcuseKind { get; set; }
        public bool WholeDays { get; set; }
        public DateTime? FromAt { get; set; }
        public DateTime? ToAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool Cancelled { get; set; }
        public string? CancelledByName { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }
    }
}
