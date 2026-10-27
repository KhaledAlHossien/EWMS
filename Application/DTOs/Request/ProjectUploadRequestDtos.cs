namespace Application.DTOs.Request
{
    /// <summary>
    /// بدء رفع وثيقة: DocumentId لإصدار جديد من وثيقة موجودة، أو Title وKind لوثيقة جديدة تُنشأ عند نجاح الإنهاء.
    /// FileName وSize هما اسم الملف وحجمه كما يعرفهما المتصفح؛ الحجم الفعلي يُتحقَّق منه عند اكتمال الأجزاء.
    /// </summary>
    public class StartProjectUploadRequestDto
    {
        public int ProjectId { get; set; }
        public int? DocumentId { get; set; }
        public string? Title { get; set; }
        public int? Kind { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public string? Source { get; set; }
        public string? Note { get; set; }
    }
}
