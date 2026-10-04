namespace Domain.Entities
{
    /// <summary>
    /// نسخة من توقيع المستخدم (صورة Data URL) — في جدول مستقل حتى لا تُحمَّل مع كل استعلام عن المستخدمين.
    /// النسخ لا تُعدَّل ولا تُحذف (قرار المستخدم 2026-10-04): كل رفع جديد نسخة جديدة تصبح الحالية،
    /// وكل قرار موقَّع يحفظ رقم النسخة التي كانت حالية وقتها (مثل Vacation.FinalApprovedSignatureId)،
    /// فلا تتغير الأوراق المعتمدة إن غيّر صاحبها توقيعه لاحقاً. "حذف التوقيع" = لا نسخة حالية.
    /// </summary>
    public class UserSignature
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string Image { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>التوقيع المستخدم حالياً (نسخة واحدة على الأكثر لكل مستخدم)</summary>
        public bool IsCurrent { get; set; }
    }
}
