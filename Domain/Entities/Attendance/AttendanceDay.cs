namespace Domain.Entities.Attendance
{
    public enum AttendanceDayType
    {
        Work = 1,
        Rest = 2,
        Holiday = 3,
        Vacation = 4
    }

    public enum AttendanceDayStatus
    {
        Pending = 1,      // يوم جارٍ لم تُغلق نوافذه بعد
        Present = 2,      // حاضر (التأخير والخروج المبكر في دقائقهما)
        Absent = 3,       // لا بصمة في أي نافذة
        Incomplete = 4,   // بصمة ناقصة (دخول بلا خروج أو العكس، أو جزء من الدوام المقسوم بلا بصمة)
        Excused = 5,      // مبرَّر كاملاً
        Rest = 6,
        Holiday = 7,
        Vacation = 8
    }

    /// <summary>
    /// نتيجة حساب يوم لشخص (سجل واحد للشخص والتاريخ). يُعاد بناؤه كاملاً عند كل حساب، ولا يُعدَّل يدوياً
    /// (التصحيح سجل مستقل يدخل الحساب). الأيام غير المجدولة وأيام خارج فترة العمل لا سجل لها.
    /// الأوقات بالتوقيت المحلي كالبصمات.
    /// </summary>
    public class AttendanceDay
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime Date { get; set; }                        // تاريخ فقط
        public AttendanceDayType Type { get; set; }
        public AttendanceDayStatus Status { get; set; }
        public int? ShiftId { get; set; }
        public AttendanceShift? Shift { get; set; }
        public DateTime? FirstIn { get; set; }
        public DateTime? LastOut { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyMinutes { get; set; }
        /// <summary>نقص الساعات في الجدول المرن</summary>
        public int ShortMinutes { get; set; }
        public int WorkMinutes { get; set; }
        public int RequiredMinutes { get; set; }
        public int PunchCount { get; set; }
        /// <summary>دخل في الحساب تبرير لفترة (إذن، مهمة…)</summary>
        public bool HasExcuse { get; set; }
        /// <summary>بصمة النتيجة: لا يُعاد الحفظ إن لم تتغير</summary>
        public string Hash { get; set; } = string.Empty;
        public DateTime CalculatedAt { get; set; }                // UTC
        public List<AttendanceDayPart> Parts { get; set; } = [];
    }

    /// <summary>تفصيل جدول زمني واحد في اليوم (أكثر من سطر في الدوام المقسوم)</summary>
    public class AttendanceDayPart
    {
        public long Id { get; set; }
        public long DayId { get; set; }
        public AttendanceDay Day { get; set; } = null!;
        public int Order { get; set; }
        public int TimetableId { get; set; }
        public string TimetableName { get; set; } = string.Empty;
        public AttendanceTimetableKind Kind { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public AttendanceDayStatus Status { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyMinutes { get; set; }
        public int ShortMinutes { get; set; }
        public int WorkMinutes { get; set; }
        public int RequiredMinutes { get; set; }
    }

    public enum AttendanceCorrectionKind
    {
        AddPunch = 1,      // بصمة يدوية (نسي البصم، أو الجهاز معطّل)
        IgnorePunch = 2,   // تجاهل بصمة خاطئة (تبقى في السجل الخام)
        Excuse = 3         // تبرير يوم أو فترة
    }

    /// <summary>أنواع التبرير — قائمة ثابتة (قرار المستخدم 2026-10-26)</summary>
    public enum AttendanceExcuseKind
    {
        Mission = 1,     // مهمة رسمية
        Permission = 2,  // إذن
        Training = 3,    // دورة تدريبية
        Other = 4
    }

    /// <summary>
    /// تصحيح يدوي مباشر (بلا طلبات ولا موافقات) بسبب إلزامي. لا يُحذف: يُلغى بسبب ويبقى ظاهراً في سجل اليوم.
    /// FromDate/ToDate = الأيام التي يمسّها (للبحث)؛ التبرير الكامل للأيام WholeDays، والجزئي فترة FromAt–ToAt في يوم واحد.
    /// </summary>
    public class AttendanceCorrection
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public AttendanceCorrectionKind Kind { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        /// <summary>البصمة اليدوية</summary>
        public DateTime? PunchTime { get; set; }
        /// <summary>البصمة المتجاهلة</summary>
        public long? PunchId { get; set; }
        public AttendancePunch? Punch { get; set; }

        public AttendanceExcuseKind? ExcuseKind { get; set; }
        public bool WholeDays { get; set; }
        public DateTime? FromAt { get; set; }
        public DateTime? ToAt { get; set; }

        public string Reason { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }                   // UTC
        public int? CancelledByUserId { get; set; }
        public User? CancelledByUser { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }
    }

    /// <summary>طابور إعادة الحساب: تحسبه الخدمة الخلفية خلال دقيقة ثم تحذفه. UserId فارغ = كل الأشخاص المراقَبين</summary>
    public class AttendanceRecalcRequest
    {
        public long Id { get; set; }
        public int? UserId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }                   // UTC
    }
}
