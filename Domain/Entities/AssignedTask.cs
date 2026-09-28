using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// مهمة يسندها رئيس لجهة أدنى منه (قسم / مكتب / موظف) وتُتابَع على لوحة المهام
    /// (لم تُنفَّذ ← قيد التنفيذ ← تم التنفيذ). مختلفة عن WorkTask (صفحات وظيفية ثابتة بلا حالات).
    /// مسار الجهة (Branch/Department/Office) مخزّن مع المهمة لتسهيل صلاحيات الاطلاع حسب النطاق.
    /// </summary>
    public class AssignedTask
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public string Description { get; set; } = string.Empty;
        public AssignedTaskPriority Priority { get; set; } = AssignedTaskPriority.Normal;
        public AssignedTaskStatus Status { get; set; } = AssignedTaskStatus.Todo;
        public DateTime? DueDate { get; set; }

        // ===== الجهة المُسندة إليها =====
        public AssignedTaskTargetType TargetType { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }
        public int? OfficeId { get; set; }
        public Office? Office { get; set; }
        public int? AssigneeUserId { get; set; }          // عند الإسناد لموظف
        public User? AssigneeUser { get; set; }

        // ===== المُسنِد =====
        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;

        // ===== التفويض: مهمة فرعية أُنشئت من مهمة أعلى =====
        public int? ParentTaskId { get; set; }
        public AssignedTask? ParentTask { get; set; }
        public ICollection<AssignedTask> SubTasks { get; set; } = new List<AssignedTask>();

        public ICollection<AssignedTaskActivity> Activities { get; set; } = new List<AssignedTaskActivity>();

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>سجل المهمة: الإنشاء، تغيير الحالة، التعليقات، التفويض، التعديل</summary>
    public class AssignedTaskActivity
    {
        public int Id { get; set; }
        public int AssignedTaskId { get; set; }
        public AssignedTask AssignedTask { get; set; } = null!;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public AssignedTaskActivityType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public AssignedTaskStatus? FromStatus { get; set; }
        public AssignedTaskStatus? ToStatus { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
