namespace Application.DTOs.Request
{
    /// <summary>
    /// إنشاء مهمة: نوع الجهة يُحدَّد من دور المُسنِد (رئيس فرع ← قسم، رئيس قسم ← مكتب، رئيس مكتب ← موظف)،
    /// لذلك يكفي إرسال معرّف الجهة. ParentTaskId عند تفويض جزء من مهمة واردة لجهة أدنى.
    /// </summary>
    public class CreateAssignedTaskRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } = 2;
        public DateTime? DueDate { get; set; }
        public int TargetId { get; set; }
        /// <summary>Department | Office | User — مطلوب إن كان للمستخدم أكثر من نوع إسناد (يُتجاهل في التفويض: نوعه من المهمة الأصل)</summary>
        public string? TargetType { get; set; }
        public int? ParentTaskId { get; set; }
    }

    public class UpdateAssignedTaskRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } = 2;
        public DateTime? DueDate { get; set; }
    }

    public class ChangeAssignedTaskStatusRequestDto
    {
        public int Status { get; set; }
    }

    public class AddAssignedTaskCommentRequestDto
    {
        public string Text { get; set; } = string.Empty;
    }
}
