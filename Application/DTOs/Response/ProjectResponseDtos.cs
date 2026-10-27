namespace Application.DTOs.Response
{
    public class ProjectResponseDto
    {
        public int Id { get; set; }
        /// <summary>الرقم الداخلي PRJ-{سنة}-{id}</summary>
        public string Number { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Beneficiary { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public int? SiteId { get; set; }
        public string? SiteName { get; set; }

        public int Stage { get; set; }
        public string StageName { get; set; } = string.Empty;
        /// <summary>مغلق أو ملغى: لا تعديل ولا مشاركين ولا وثائق جديدة حتى تُعاد فتحه بتغيير المرحلة</summary>
        public bool IsFinished { get; set; }

        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public int MembersCount { get; set; }
        /// <summary>تعديل المشروع: يملك المستخدم EditProject (والمشروع ضمن حدّه دائماً هنا)</summary>
        public bool CanEdit { get; set; }
        /// <summary>المشاركون — في تفاصيل المشروع فقط (فارغ في القوائم)</summary>
        public List<ProjectMemberResponseDto>? Members { get; set; }
    }

    public class ProjectMemberResponseDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
    }

    /// <summary>موظف يصلح مشاركاً: نشط من فرع المشروع ولم يُضف بعد</summary>
    public class ProjectMemberCandidateDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
    }

    public class AuditLogResponseDto
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public int? ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
