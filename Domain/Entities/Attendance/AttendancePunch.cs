namespace Domain.Entities.Attendance
{
    public enum AttendancePunchSource
    {
        Push = 1,      // أرسله الجهاز فوراً
        Query = 2,     // سُحب من ذاكرة الجهاز بأمر
        Import = 3     // نُقل من BioTime
    }

    /// <summary>
    /// بصمة خام كما أرسلها الجهاز: لا تُعدَّل ولا تُحذف أبداً (التصحيح في المرحلة الثانية سجل مستقل).
    /// المكرر (الجهاز، الرقم، الوقت) يُتجاهل، فإعادة الإرسال والسحب آمنان. الوقت بتوقيت الجهاز المحلي كما وصل.
    /// </summary>
    public class AttendancePunch
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public AttendanceDevice Device { get; set; } = null!;
        public string DeviceSerial { get; set; } = string.Empty;
        /// <summary>رقم البصمة كما وصل (يبقى حتى إن لم يُعرف صاحبه)</summary>
        public string Pin { get; set; } = string.Empty;
        /// <summary>صاحب الرقم وقت الاستلام؛ فارغ = رقم غير معروف (يُربط بإجراء صريح فقط)</summary>
        public int? UserId { get; set; }
        public User? User { get; set; }
        public DateTime PunchTime { get; set; }
        /// <summary>حالة البصمة كما يرسلها الجهاز (0 دخول، 1 خروج، …) — للعرض فقط، والحساب في المرحلة الثانية</summary>
        public int Status { get; set; }
        /// <summary>طريقة التحقق: 1 إصبع، 4 بطاقة، 15 وجه، … (AttendanceVerify)</summary>
        public int VerifyType { get; set; }
        public string WorkCode { get; set; } = string.Empty;
        public AttendancePunchSource Source { get; set; } = AttendancePunchSource.Push;
        public DateTime ReceivedAt { get; set; }       // UTC
    }

    /// <summary>
    /// صورة لحظة البصم (ATTPHOTO) — تُحفظ مدة محدودة (Attendance:PhotoRetentionDays، الافتراضي 90) ثم تُحذف،
    /// والبصمة نفسها تبقى. المحتوى في مخزن المرفقات (StorageKey) أو في جدول المحتوى في وضع Database.
    /// </summary>
    public class AttendancePunchPhoto
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public AttendanceDevice Device { get; set; } = null!;
        public string DeviceSerial { get; set; } = string.Empty;
        public string Pin { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public DateTime PunchTime { get; set; }
        public int Size { get; set; }
        public string? StorageKey { get; set; }
        public DateTime ReceivedAt { get; set; }       // UTC
        public AttendancePunchPhotoContent? Content { get; set; }
    }

    public class AttendancePunchPhotoContent
    {
        public long PhotoId { get; set; }
        public AttendancePunchPhoto Photo { get; set; } = null!;
        public byte[] Data { get; set; } = [];
    }

    /// <summary>نوع القالب الحيوي كما في البروتوكول (BIODATA Type)</summary>
    public enum AttendanceBiometricType
    {
        Finger = 1,
        Face = 2,           // وجه بالأشعة تحت الحمراء (أجهزة قديمة)
        Palm = 8,
        VisibleFace = 9     // وجه بالضوء المرئي (SpeedFace)
    }

    /// <summary>
    /// قالب وجه أو إصبع سُجّل على أحد أجهزة المنطقة، يُوزَّع على بقية أجهزتها ذات الخوارزمية نفسها.
    /// بيانات حيوية حساسة: المحتوى مشفّر بالمفتاح Attendance:TemplateKey (AES-GCM).
    /// </summary>
    public class AttendanceBiometric
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public AttendanceBiometricType Type { get; set; }
        /// <summary>رقم الإصبع (0–9) أو 0 للوجه</summary>
        public int No { get; set; }
        public int Index { get; set; }
        public bool IsValid { get; set; } = true;
        public bool Duress { get; set; }
        public string MajorVersion { get; set; } = string.Empty;
        public string MinorVersion { get; set; } = string.Empty;
        public int Format { get; set; }
        public byte[] Data { get; set; } = [];
        /// <summary>SHA-256 للقالب قبل التشفير: يكشف التغيير دون فكّه</summary>
        public string Hash { get; set; } = string.Empty;
        public int? SourceDeviceId { get; set; }
        public AttendanceDevice? SourceDevice { get; set; }
        public DateTime UpdatedAt { get; set; }        // UTC
    }

    /// <summary>
    /// صورة تسجيل الوجه (BIOPHOTO)، واحدة لكل شخص ومشفّرة: جهاز بموديل أو خوارزمية مختلفة يستخرج منها قالبه بنفسه.
    /// </summary>
    public class AttendanceBioPhoto
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public byte[] Data { get; set; } = [];
        public string Hash { get; set; } = string.Empty;
        public int Size { get; set; }
        public int? SourceDeviceId { get; set; }
        public AttendanceDevice? SourceDevice { get; set; }
        public DateTime UpdatedAt { get; set; }        // UTC
    }
}
