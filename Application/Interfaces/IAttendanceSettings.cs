namespace Application.Interfaces
{
    /// <summary>إعدادات وحدة الحضور (القسم Attendance في الإعدادات) — تُقرأ مرة عند الإقلاع</summary>
    public interface IAttendanceSettings
    {
        /// <summary>Attendance:Enabled (الافتراضي false): بدونها لا تُستقبل الأجهزة ولا تعمل الخدمة الخلفية</summary>
        bool Enabled { get; }
        /// <summary>جهاز بلا اتصال منذ هذه المدة يُعدّ منقطعاً ويُرسل إشعاره (الافتراضي 10 دقائق)</summary>
        int OfflineMinutes { get; }
        /// <summary>فترة استطلاع الجهاز للخادم بالثواني (Delay في المصافحة، الافتراضي 10)</summary>
        int PollSeconds { get; }
        /// <summary>أقصى عدد أوامر في رد استطلاع واحد (الافتراضي 20)</summary>
        int CommandsPerPoll { get; }
        /// <summary>مدة الاحتفاظ بصور لحظة البصم بالأيام (الافتراضي 90)</summary>
        int PhotoRetentionDays { get; }
        /// <summary>فرق توقيت الأجهزة عن UTC بالساعات (الافتراضي 3 = سوريا)</summary>
        int TimeZoneHours { get; }
        /// <summary>طلبات الأجهزة لا تُقبل إلا عبر بوابة الأجهزة (Nginx على 8081 يضيف X-EWMS-Device-Gateway) — true في الإنتاج</summary>
        bool RequireGateway { get; }
        /// <summary>منفذ الأجهزة المباشر في التطوير (Kestrel على 8081): ما يصله لا يخدم إلا /iclock</summary>
        int DevicePort { get; }
        /// <summary>مجلد سجل التشخيص: كل طلب خام من الأجهزة يُكتب فيه (فارغ = معطّل) — للتحقق من البروتوكول على جهاز جديد</summary>
        string? RawLogFolder { get; }
    }
}
