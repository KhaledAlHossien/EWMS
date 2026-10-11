namespace Domain.Entities
{
    /// <summary>حالة التركيب: «أُزيل» يُبقي السجل بعد فك الجهاز (الحذف لأخطاء الإدخال فقط)</summary>
    public enum InstallationStatus
    {
        Active = 1,   // يعمل
        Faulty = 2,   // معطّل
        Removed = 3   // أُزيل
    }

    // جدول كسر العلاقة بين Device و Site (many-to-many) + بيانات الاتصال بالجهاز في هذا الموقع تحديداً
    public class DeviceSite
    {
        public int Id { get; set; }

        public required int DeviceId { get; set; }
        public Device Device { get; set; } = null!;

        public required int SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public string Ip { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// كلمة سر الجهاز مشفّرة (AES-GCM، المفتاح DeviceInventory:PasswordKey) بالبادئة enc:v1: —
        /// لا تُرسل في القوائم أبداً، وتُقرأ فقط من DeviceSites/Password/{id} بصلاحية RevealDevicePasswords مع تسجيل كل إظهار.
        /// </summary>
        public string Pass { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;

        // الرقم التسلسلي للقطعة المركّبة (الجهاز نوع قابل للتكرار، ولكل قطعة رقمها) — قرار المستخدم 2026-09-29
        public string SN { get; set; } = string.Empty;

        // وصف دقيق لمكان التركيب داخل الموقع (مثل: عند البوابة الرئيسية)
        public string InstallLocation { get; set; } = string.Empty;

        // ===== بيانات الشبكة والتشغيل (2026-10-05) =====
        public string MacAddress { get; set; } = string.Empty;   // AA:BB:CC:DD:EE:FF
        public string Port { get; set; } = string.Empty;         // منفذ السويتش (مثل Gi0/12)
        public int? Vlan { get; set; }
        public string Firmware { get; set; } = string.Empty;
        public DateTime? InstallDate { get; set; }
        /// <summary>آخر مرة تحقق فيها أحد من صحة بيانات التركيب</summary>
        public DateTime? LastVerifiedAt { get; set; }
        public InstallationStatus Status { get; set; } = InstallationStatus.Active;

        // ===== الوحدة المالكة (قرار المستخدم 2026-10-11: ما يضيفه الموظف يرتبط بقسمه وفرعه) =====
        // تُؤخذ من قسم من أضاف التركيب وفرعه، وتحدد من يراه (قسمي/فرعي) وعلى خريطة أي فرع يظهر.
        // null للتركيبات القديمة التي لم يُعرف صاحبها (يراها من يملك نظرة المؤسسة ويحدد قسمها من التعديل).
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}
