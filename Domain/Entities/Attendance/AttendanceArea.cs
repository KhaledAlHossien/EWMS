namespace Domain.Entities.Attendance
{
    /// <summary>
    /// منطقة الدوام (قرار المستخدم 2026-10-26، على فكرة «المنطقة» في BioTime): مجموعة فروع يراقب حضورها شخص واحد.
    /// كل فرع في منطقة واحدة على الأكثر (Branch.AttendanceAreaId)، وأجهزة البصمة تتبع المنطقة لا الفرع،
    /// وكل جهاز يحمل كل أشخاص منطقته. «منطقتي» لمن يراقب = منطقة الفرع الذي يتبع له.
    /// </summary>
    public class AttendanceArea
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;          // فريد
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// بيانات حضور شخص (سجل واحد لكل مستخدم يُنشأ عند أول إدخال): رقم البصمة على الأجهزة وما يتعلق بها.
    /// منطقة الموظف = منطقة فرعه؛ AreaId لعامل المياومة وحده (يتبع المنطقة التي يعمل فيها، لا فرعاً بالضرورة).
    /// </summary>
    public class AttendanceProfile
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>رقم البصمة (PIN على الجهاز): أرقام فقط، فريد في المؤسسة كلها. الفارغ = لا يُحمَّل على أي جهاز</summary>
        public string? AttendanceNumber { get; set; }
        /// <summary>رقم بطاقة الدخول (قارئ ID في الجهاز) — اختياري وفريد</summary>
        public string? CardNumber { get; set; }
        /// <summary>مدير على الجهاز (يفتح قائمته ويسجّل البصمات) — Pri=14 في البروتوكول</summary>
        public bool IsDeviceAdmin { get; set; }

        /// <summary>منطقة عامل المياومة (إلزامية له)، وفارغة للموظف (تؤخذ من فرعه)</summary>
        public int? AreaId { get; set; }
        public AttendanceArea? Area { get; set; }

        /// <summary>فترة العمل (للعامل خاصة): خارجها لا يُحمَّل على الأجهزة ولا يُحسب غائباً</summary>
        public DateTime? ActiveFrom { get; set; }
        public DateTime? ActiveTo { get; set; }

        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        /// <summary>مناطق إضافية لمن يبصم في غير منطقته</summary>
        public List<AttendanceProfileArea> ExtraAreas { get; set; } = [];
    }

    public class AttendanceProfileArea
    {
        public int UserId { get; set; }
        public AttendanceProfile Profile { get; set; } = null!;
        public int AreaId { get; set; }
        public AttendanceArea Area { get; set; } = null!;
    }
}
