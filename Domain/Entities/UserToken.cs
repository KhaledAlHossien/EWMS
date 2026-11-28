namespace Domain.Entities
{
    public class UserToken
    {
        public int Id { get; set; }

        public required int UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>بصمة SHA-256 للرمز (Application.Common.TokenHash) — الرمز نفسه لا يُحفظ</summary>
        public required string TokenHash { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}
