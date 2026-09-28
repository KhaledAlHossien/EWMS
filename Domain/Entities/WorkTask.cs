namespace Domain.Entities
{
    /// <summary>
    /// مهمة عمل دورية/ثابتة يقوم بها الفرع (مثال: إدارة المخزن — إدخال، إخراج، تقارير).
    /// كل فرع له مهامه الخاصة، وتُسند لموظفين من نفس الفرع.
    /// صفحة كل مهمة تُبنى لاحقاً؛ حتى ذلك الحين تفتح بطاقتها صفحة "جاري العمل عليها".
    /// </summary>
    public class WorkTask
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "📋";

        public required int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        public ICollection<UserWorkTask> Assignments { get; set; } = new List<UserWorkTask>();
    }
}
