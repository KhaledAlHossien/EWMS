namespace Domain.Entities.Attendance
{
    /// <summary>حالة جهاز البصمة في السجل</summary>
    public enum AttendanceDeviceState
    {
        Pending = 1,    // اتصل أول مرة ولم يُقبل بعد: بياناته مرفوضة (تبقى في ذاكرته)
        Approved = 2,   // مقبول: يتبع منطقة وتُستقبل بياناته وتُرسل إليه الأوامر
        Rejected = 3,   // مرفوض: يُتجاهل بصمت
        Removed = 4     // أُزيل (حذف ناعم): يبقى في السجل وتبقى بصماته، وإن اتصل ثانية يعود «بانتظار الموافقة»
    }

    /// <summary>
    /// جهاز بصمة يتصل بالخادم بطريقة Push/ADMS (المنفذ 8081). يعرّف نفسه برقمه التسلسلي فقط، فالجهاز الجديد
    /// يُسجَّل «بانتظار الموافقة» ولا تُقبل بياناته حتى يقبله من يملك ApproveAttendanceDevice ويحدد منطقته.
    /// </summary>
    public class AttendanceDevice
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;   // فريد
        public string Name { get; set; } = string.Empty;
        public AttendanceDeviceState State { get; set; } = AttendanceDeviceState.Pending;

        public int? AreaId { get; set; }
        public AttendanceArea? Area { get; set; }

        // ── ما يبلّغ به الجهاز نفسه ──
        public string? Model { get; set; }
        public string? Firmware { get; set; }
        public string? PushVersion { get; set; }
        /// <summary>إصدار خوارزمية الإصبع والوجه: القالب لا يُرسل إلا لجهاز بالخوارزمية نفسها</summary>
        public string? FingerAlgorithm { get; set; }
        public string? FaceAlgorithm { get; set; }
        public string? IpAddress { get; set; }
        public int? UserCount { get; set; }
        public int? FingerCount { get; set; }
        public int? FaceCount { get; set; }
        public int? PunchCount { get; set; }

        /// <summary>مؤشرات آخر ما استلمناه من كل جدول (يعيدها الخادم للجهاز عند المصافحة)</summary>
        public string AttLogStamp { get; set; } = "0";
        public string OperLogStamp { get; set; } = "0";
        public string PhotoStamp { get; set; } = "0";

        public DateTime FirstSeenAt { get; set; }      // UTC
        public DateTime? LastSeenAt { get; set; }      // UTC
        /// <summary>أُرسل إشعار انقطاعه (يُصفَّر عند عودته)</summary>
        public DateTime? OfflineNotifiedAt { get; set; }
        /// <summary>تغيّر شيء يخص من يجب أن يكون على الجهاز: تعيد الخدمة الخلفية حساب المزامنة</summary>
        public bool NeedsSync { get; set; }

        public string Notes { get; set; } = string.Empty;
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public enum AttendanceCommandKind
    {
        SyncUser = 1,          // إضافة شخص أو تحديثه على الجهاز
        DeleteUser = 2,        // حذف شخص من الجهاز (وكل قوالبه)
        SyncBiometric = 3,     // إرسال قالب وجه أو إصبع
        SyncBioPhoto = 4,      // إرسال صورة تسجيل الوجه (يستخرج منها الجهاز قالبه)
        QueryPunches = 5,      // سحب السجلات من ذاكرة الجهاز لفترة
        QueryBiometrics = 6,   // سحب القوالب المسجّلة على الجهاز
        RefreshInfo = 7,       // تحديث معلومات الجهاز وأعداده
        SetTime = 8,           // ضبط الوقت على وقت الخادم
        Reboot = 9,            // إعادة التشغيل
        QueryUsers = 10        // سحب قائمة المستخدمين على الجهاز
    }

    public enum AttendanceCommandState
    {
        Queued = 1,
        Sent = 2,
        Succeeded = 3,
        Failed = 4,
        Expired = 5
    }

    /// <summary>
    /// طابور أوامر الجهاز: تُرسل في رد الاستطلاع (getrequest) بصيغة C:&lt;Id&gt;:&lt;النص&gt;، وتعود نتيجتها على devicecmd.
    /// لا يوجد أمر يمسح بيانات الجهاز.
    /// </summary>
    public class AttendanceDeviceCommand
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public AttendanceDevice Device { get; set; } = null!;
        public AttendanceCommandKind Kind { get; set; }
        public string Text { get; set; } = string.Empty;
        /// <summary>وصف مقروء للأمر في سجل الجهاز</summary>
        public string Summary { get; set; } = string.Empty;
        /// <summary>الشخص المعني بالأمر (مزامنة، حذف، قالب) إن وُجد</summary>
        public int? UserId { get; set; }
        public AttendanceCommandState State { get; set; } = AttendanceCommandState.Queued;
        public int? ReturnCode { get; set; }
        /// <summary>من طلبه (فارغ = الخدمة الخلفية)</summary>
        public int? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }
        public DateTime CreatedAt { get; set; }        // UTC
        public DateTime? SentAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// من على الجهاز: من وضعناه نحن (Managed) ومن وجدناه عليه وليس عندنا (UserId فارغ). المزامنة لا تحذف إلا ما وضعته.
    /// </summary>
    public class AttendanceDeviceUser
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public AttendanceDevice Device { get; set; } = null!;
        public string Pin { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public User? User { get; set; }
        /// <summary>الاسم كما على الجهاز (لمن ليس عندنا)</summary>
        public string Name { get; set; } = string.Empty;
        public bool Managed { get; set; }
        /// <summary>بصمة آخر بيانات أُرسلت (الاسم، البطاقة، الصلاحية) وآخر قوالب أُرسلت — تغيّرها يعيد الإرسال</summary>
        public string? SyncedHash { get; set; }
        public string? BiometricHash { get; set; }
        public DateTime? SyncedAt { get; set; }
    }

    public enum AttendanceDeviceEvent
    {
        FirstSeen = 1,
        Approved = 2,
        Rejected = 3,
        Edited = 4,
        Removed = 5,
        IpChanged = 6,
        WentOffline = 7,
        CameOnline = 8,
        Command = 9
    }

    /// <summary>سجل الجهاز: ظهوره وقبوله وتعديله وتغيّر عنوانه وانقطاعه والأوامر اليدوية</summary>
    public class AttendanceDeviceLog
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public AttendanceDevice Device { get; set; } = null!;
        public AttendanceDeviceEvent Event { get; set; }
        public string Details { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public User? User { get; set; }
        public DateTime CreatedAt { get; set; }        // UTC
    }
}
