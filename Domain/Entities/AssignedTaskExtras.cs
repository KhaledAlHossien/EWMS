namespace Domain.Entities
{
    /// <summary>
    /// مرفق مهمة (قرار المستخدم 2026-10-07): PDF أو صورة JPG/PNG أو مستند Office، حتى 10 ملفات × 10MB لكل مهمة، يُرفق في أي وقت قبل «تم التنفيذ».
    /// البيانات الوصفية هنا، والمحتوى في AssignedTaskAttachmentContent (جدول مستقل) كي لا يُحمَّل الملف مع لوحة المهام.
    /// </summary>
    public class AssignedTaskAttachment
    {
        public int Id { get; set; }
        public int AssignedTaskId { get; set; }
        public AssignedTask AssignedTask { get; set; } = null!;

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;   // يُحدَّد من محتوى الملف لا من المتصفح
        public int Size { get; set; }

        public int UploadedByUserId { get; set; }
        public User UploadedByUser { get; set; } = null!;
        public DateTime UploadedAt { get; set; }

        public AssignedTaskAttachmentContent Content { get; set; } = null!;
    }

    /// <summary>محتوى المرفق (يُقرأ فقط عند فتحه أو تنزيله)</summary>
    public class AssignedTaskAttachmentContent
    {
        public int AttachmentId { get; set; }
        public byte[] Data { get; set; } = [];
    }

    /// <summary>بند في قائمة تحقق المهمة (خطوات صغيرة داخل المهمة نفسها، بلا مهام فرعية)</summary>
    public class AssignedTaskChecklistItem
    {
        public int Id { get; set; }
        public int AssignedTaskId { get; set; }
        public AssignedTask AssignedTask { get; set; } = null!;

        public string Text { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public DateTime? DoneAt { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
