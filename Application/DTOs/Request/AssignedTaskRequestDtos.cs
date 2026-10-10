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
        /// <summary>بنود تحقق تُنشأ مع المهمة (من قالب أو يدوياً)</summary>
        public List<string>? ChecklistItems { get; set; }
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
        /// <summary>سبب إعادة المهمة من المراجعة إلى التنفيذ (من المُسنِد) — مطلوب عندها</summary>
        public string? Note { get; set; }
        /// <summary>الحالة التي رآها المستخدم حين قرّر؛ إن تغيّرت منذها يُرفض القرار ويُطلب تحديث الصفحة</summary>
        public int? ExpectedStatus { get; set; }
    }

    public class AddChecklistItemRequestDto
    {
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>تعديل بند: التعليم (isDone) و/أو النص (للمُسنِد)</summary>
    public class UpdateChecklistItemRequestDto
    {
        public bool? IsDone { get; set; }
        public string? Text { get; set; }
    }

    public class ClaimAssignedTaskRequestDto
    {
        /// <summary>true = أتولّى المهمة، false = أتخلّى عنها</summary>
        public bool Claim { get; set; } = true;
    }

    public class AddAssignedTaskCommentRequestDto
    {
        public string Text { get; set; } = string.Empty;
    }
}
