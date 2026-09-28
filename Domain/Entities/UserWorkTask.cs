namespace Domain.Entities
{
    /// <summary>إسناد مهمة عمل لموظف (علاقة متعدد-لمتعدد)</summary>
    public class UserWorkTask
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int WorkTaskId { get; set; }
        public WorkTask WorkTask { get; set; } = null!;

        public DateTime AssignedAt { get; set; }
    }
}
