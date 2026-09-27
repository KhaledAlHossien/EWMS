using Domain.Enums;

namespace Domain.Entities
{
    public class Notification
    {
        public int Id { get; set; }

        // المستلم
        public required int UserId { get; set; }
        public User User { get; set; } = null!;

        public required string Title { get; set; }
        public required string Message { get; set; }

        public NotificationType Type { get; set; }

        // لفتح العنصر المرتبط مباشرة من الواجهة (مثلاً تفاصيل إجازة معيّنة)
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
