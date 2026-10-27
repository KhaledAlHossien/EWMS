namespace Application.DTOs.Response
{
    public class ProjectDocumentVersionResponseDto
    {
        public int Id { get; set; }
        public int VersionNumber { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int UploadedByUserId { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public bool IsApproved { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        /// <summary>صورة أو PDF: تُعرض داخل الصفحة، وغيرها تُنزَّل</summary>
        public bool Previewable { get; set; }
        /// <summary>يستطيع المستخدم الحالي اعتماده: يملك الصلاحية، والإصدار غير معتمد، وليس هو من رفعه</summary>
        public bool CanApprove { get; set; }
    }

    public class ProjectDocumentResponseDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Kind { get; set; }
        public string KindName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int VersionsCount { get; set; }
        /// <summary>آخر إصدار مرفوع</summary>
        public ProjectDocumentVersionResponseDto? Latest { get; set; }
        /// <summary>أحدث إصدار معتمد (قد يكون أقدم من الأخير)</summary>
        public ProjectDocumentVersionResponseDto? Approved { get; set; }
        /// <summary>كل الإصدارات (الأحدث أولاً) — في تفاصيل الوثيقة فقط (فارغ في القوائم)</summary>
        public List<ProjectDocumentVersionResponseDto>? Versions { get; set; }
        public bool CanDelete { get; set; }
    }
}
