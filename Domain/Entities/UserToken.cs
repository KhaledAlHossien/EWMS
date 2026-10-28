namespace Domain.Entities
{
    public class UserToken
    {
        public int Id { get; set; }

        public required int UserId { get; set; }
        public User User { get; set; } = null!;

        public required string Token { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        /// <summary>بداية الجلسة الأصلية (وقت الدخول): التجديد يحمل هذا التاريخ إلى التوكن الجديد، فيُقطع التجديد عند الحد الأقصى لعمر الجلسة. null = توكن قديم، يُعدّ CreatedAt.</summary>
        public DateTime? SessionStartedAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}