using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Attendance
{
    /// <summary>القسم Attendance من الإعدادات — القيم خارج حدودها تُقصّ إلى أقرب حد معقول</summary>
    public sealed class AttendanceSettings : IAttendanceSettings
    {
        public AttendanceSettings(IConfiguration configuration)
        {
            var section = configuration.GetSection("Attendance");
            Enabled = section.GetValue("Enabled", false);
            OfflineMinutes = Math.Clamp(section.GetValue("OfflineMinutes", 10), 2, 1440);
            PollSeconds = Math.Clamp(section.GetValue("PollSeconds", 10), 5, 300);
            CommandsPerPoll = Math.Clamp(section.GetValue("CommandsPerPoll", 20), 1, 200);
            PhotoRetentionDays = Math.Clamp(section.GetValue("PhotoRetentionDays", 90), 1, 3650);
            TimeZoneHours = Math.Clamp(section.GetValue("TimeZoneHours", 3), -12, 14);
            RequireGateway = section.GetValue("RequireGateway", true);
            DevicePort = section.GetValue("DevicePort", 0);
            var folder = section["RawLogFolder"]?.Trim();
            RawLogFolder = string.IsNullOrEmpty(folder) ? null : Path.GetFullPath(folder);
            TemplateKey = section["TemplateKey"];
        }

        public bool Enabled { get; }
        public int OfflineMinutes { get; }
        public int PollSeconds { get; }
        public int CommandsPerPoll { get; }
        public int PhotoRetentionDays { get; }
        public int TimeZoneHours { get; }
        public bool RequireGateway { get; }
        public int DevicePort { get; }
        public string? RawLogFolder { get; }
        /// <summary>مفتاح تشفير القوالب الحيوية (32 بايت Base64) — يقرؤه AttendanceTemplateProtector</summary>
        public string? TemplateKey { get; }

        /// <summary>مشكلة تمنع الإقلاع (للرسالة العربية في StartupSettings)، أو null — المفتاح مطلوب فقط إن فُعّلت الوحدة</summary>
        public string? Problem()
        {
            if (!Enabled) return null;
            try
            {
                if (!string.IsNullOrWhiteSpace(TemplateKey) && Convert.FromBase64String(TemplateKey).Length == 32) return null;
            }
            catch (FormatException) { }
            return "مفتاح تشفير قوالب البصمة (Attendance__TemplateKey): 32 بايت بصيغة Base64 — مطلوب عند Attendance__Enabled=true";
        }
    }
}
