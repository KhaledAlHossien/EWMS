namespace Application.DTOs.Response
{
    /// <summary>حالة جلسة رفع: الواجهة تستأنف من Received، وتستعلم أثناء الإنهاء حتى Done أو Failed</summary>
    public class ProjectUploadResponseDto
    {
        public Guid Id { get; set; }
        public int ProjectId { get; set; }
        public int? DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Kind { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public long Received { get; set; }
        /// <summary>أكبر جزء يقبله الخادم (إعداد الخادم)</summary>
        public long ChunkSize { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        /// <summary>الإصدار الناتج بعد النجاح</summary>
        public int? VersionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
