namespace Domain.Entities
{
    /// <summary>
    /// صورة توقيع المستخدم (Data URL) — في جدول مستقل حتى لا تُحمَّل مع كل استعلام عن المستخدمين.
    /// تُطبع حالياً على ورقة تسليم طلب الصيانة (توقيع رئيس القسم).
    /// </summary>
    public class UserSignature
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string Image { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
