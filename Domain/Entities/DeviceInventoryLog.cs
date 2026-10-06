namespace Domain.Entities
{
    public enum DeviceInventoryEntity
    {
        Site = 1,
        Device = 2,
        Installation = 3
    }

    public enum DeviceInventoryAction
    {
        Created = 1,
        Updated = 2,
        Deleted = 3,
        PasswordRevealed = 4,   // إظهار كلمة السر على الشاشة
        PasswordCopied = 5,     // نسخ كلمة السر
        Verified = 6,           // «تحققت اليوم» من بيانات التركيب
        Imported = 7            // أُضيف من ملف Excel
    }

    /// <summary>
    /// سجل توثيق الأجهزة: من فعل ماذا ومتى، مع القيم قبل التغيير وبعده (كلمة السر تُسجَّل «تغيّرت» فقط).
    /// لا يُحذف مع السجل المعني (يبقى أثر الحذف)، والمستخدم Restrict (حماية حذف المستخدم).
    /// </summary>
    public class DeviceInventoryLog
    {
        public int Id { get; set; }
        public DeviceInventoryEntity EntityType { get; set; }
        public int EntityId { get; set; }
        public DeviceInventoryAction Action { get; set; }

        /// <summary>اسم مقروء للسجل لحظة الإجراء (يبقى بعد الحذف)</summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>التغييرات سطراً لكل حقل: «IP: 10.0.0.1 ← 10.0.0.2»</summary>
        public string Details { get; set; } = string.Empty;

        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
