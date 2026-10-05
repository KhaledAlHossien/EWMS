namespace Domain.Entities
{
    /// <summary>
    /// مرفق طلب إجازة (قرار المستخدم 2026-10-05): PDF أو صورة JPG/PNG، حتى 3 ملفات × 5MB، يُرفق عند التقديم فقط.
    /// البيانات الوصفية هنا، والمحتوى في VacationAttachmentContent (جدول مستقل) كي لا يُحمَّل الملف مع قوائم الإجازات.
    /// </summary>
    public class VacationAttachment
    {
        public int Id { get; set; }

        public int VacationId { get; set; }
        public Vacation Vacation { get; set; } = null!;

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;   // يُحدَّد من محتوى الملف لا من المتصفح
        public int Size { get; set; }
        public DateTime UploadedAt { get; set; }

        public VacationAttachmentContent Content { get; set; } = null!;
    }

    /// <summary>محتوى المرفق (يُقرأ فقط عند فتحه أو تنزيله)</summary>
    public class VacationAttachmentContent
    {
        public int AttachmentId { get; set; }
        public byte[] Data { get; set; } = [];
    }
}
