namespace Domain.Entities.Attendance
{
    public enum AttendanceTimetableKind
    {
        Fixed = 1,      // وقت دخول ووقت خروج محددان
        Flexible = 2    // عدد ساعات مطلوب داخل نافذة اليوم، بلا وقت دخول محدد
    }

    /// <summary>طريقة عدّ دقائق التأخير والخروج المبكر متى تجاوزت السماح (قرار المستخدم 2026-10-26: خيار في كل جدول)</summary>
    public enum AttendanceLateCounting
    {
        FromStart = 1,   // سماح 10 ودخول 8:15 ← 15 دقيقة
        AfterGrace = 2   // المثال نفسه ← 5 دقائق
    }

    /// <summary>
    /// الجدول الزمني (فترة عمل واحدة) — الطبقة الأولى من نموذج الورديات (على فكرة BioTime، مكتوب من الصفر).
    /// الأوقات بالدقائق من منتصف الليل؛ خروج قبل الدخول (أو مساوٍ له) = جدول ليلي يعبر منتصف الليل، وينتمي اليوم إلى تاريخ الدخول.
    /// النوافذ بالدقائق قبل وقت الدخول/الخروج وبعده: البصمة خارجها لا تُعدّ دخولاً أو خروجاً لهذا الجدول.
    /// </summary>
    public class AttendanceTimetable
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;          // فريد
        public AttendanceTimetableKind Kind { get; set; }
        public string Color { get; set; } = "#2E7D32";

        /// <summary>الثابت: وقت الدخول والخروج. المرن: بداية نافذة اليوم ونهايتها</summary>
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }

        // نوافذ قبول البصمة (الثابت فقط)
        public int CheckInBeforeMinutes { get; set; }
        public int CheckInAfterMinutes { get; set; }
        public int CheckOutBeforeMinutes { get; set; }
        public int CheckOutAfterMinutes { get; set; }

        public int LateGraceMinutes { get; set; }
        public int EarlyLeaveGraceMinutes { get; set; }
        public AttendanceLateCounting LateCounting { get; set; }

        /// <summary>بصمة الدخول/الخروج إلزامية؛ غير الإلزامية إن غابت تُعدّ في وقتها</summary>
        public bool RequireCheckIn { get; set; }
        public bool RequireCheckOut { get; set; }

        public int BreakMinutes { get; set; }
        /// <summary>المرن: دقائق العمل المطلوبة</summary>
        public int RequiredMinutes { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public enum AttendanceCycleUnit
    {
        Week = 1,   // دورة بالأسابيع، تبدأ من السبت
        Day = 2     // دورة بعدد أيام (الورديات الدوّارة)
    }

    /// <summary>
    /// الوردية = دورة من أيام، لكل يوم جدول زمني أو أكثر (الدوام المقسوم)، واليوم الذي بلا جدول يوم راحة.
    /// رقم اليوم في الدورة يُحسب من StartDate (للأسابيع: من سبت أسبوعه).
    /// </summary>
    public class AttendanceShift
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;          // فريد
        public string Description { get; set; } = string.Empty;
        public AttendanceCycleUnit CycleUnit { get; set; }
        /// <summary>عدد الأسابيع (1–4) أو الأيام (1–60)</summary>
        public int CycleLength { get; set; }
        public DateTime StartDate { get; set; }                   // تاريخ فقط
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public List<AttendanceShiftDay> Days { get; set; } = [];
    }

    public class AttendanceShiftDay
    {
        public int Id { get; set; }
        public int ShiftId { get; set; }
        public AttendanceShift Shift { get; set; } = null!;
        /// <summary>0 = أول يوم في الدورة (السبت لدورة الأسابيع)</summary>
        public int DayIndex { get; set; }
        public int TimetableId { get; set; }
        public AttendanceTimetable Timetable { get; set; } = null!;
    }

    /// <summary>الجهة التي تُسند إليها وردية — الأخص يغلب: شخص، مكتب، قسم، فرع، منطقة</summary>
    public enum AttendanceScheduleTarget
    {
        Area = 1,
        Branch = 2,
        Department = 3,
        Office = 4,
        Person = 5
    }

    /// <summary>
    /// إسناد وردية لجهة من تاريخ إلى تاريخ (النهاية اختيارية). إسنادات الجهة الواحدة لا تتداخل.
    /// رقم الجهة بلا مفتاح أجنبي (مثل التكرار في لوحة المهام): إسناد جهة محذوفة لا ينطبق على أحد ويظهر «جهة محذوفة».
    /// </summary>
    public class AttendanceSchedule
    {
        public int Id { get; set; }
        public AttendanceScheduleTarget TargetType { get; set; }
        public int TargetId { get; set; }
        public int ShiftId { get; set; }
        public AttendanceShift Shift { get; set; } = null!;
        public DateTime FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Note { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int? UpdatedByUserId { get; set; }
        public User? UpdatedByUser { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// جدولة مؤقتة ليوم أو أيام: جدول زمني بديل، أو راحة (TimetableId فارغ). تغلب كل إسناد عادي،
    /// وبين المؤقتات الأخص يغلب. مؤقتات الجهة الواحدة لا تتداخل.
    /// </summary>
    public class AttendanceTemporarySchedule
    {
        public int Id { get; set; }
        public AttendanceScheduleTarget TargetType { get; set; }
        public int TargetId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? TimetableId { get; set; }
        public AttendanceTimetable? Timetable { get; set; }
        public string Note { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
