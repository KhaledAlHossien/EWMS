namespace Application.DTOs.Request
{
    public class CorrespondenceTypeRequestDto
    {
        /// <summary>فرع النوع: فرع المستخدم، ولنظرة المؤسسة أي فرع</summary>
        public int? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? DeadlineDays { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public class CorrespondenceStatusRequestDto
    {
        public int? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>CorrespondenceStage: 1 مسجّلة، 2 قيد المعالجة، 3 تمّت الإجابة، 4 مغلقة</summary>
        public int Stage { get; set; } = 1;
        public string Color { get; set; } = "#8E8E93";
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    /// <summary>تسجيل وارد جديد (الحالة الابتدائية أول حالة فعّالة بمرحلة «مسجّلة»، والوحدة الحاملة وحدة المسجِّل)</summary>
    public class RegisterIncomingRequestDto
    {
        public int? BranchId { get; set; }
        public int TypeId { get; set; }
        public string Party { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? ExternalNumber { get; set; }
        public DateTime? ExternalDate { get; set; }
        public int? ProjectId { get; set; }
    }

    /// <summary>مسودة كتاب صادر</summary>
    public class CreateOutgoingDraftRequestDto
    {
        public int? BranchId { get; set; }
        public int TypeId { get; set; }
        public string Party { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? ExternalNumber { get; set; }
        public int? ProjectId { get; set; }
        /// <summary>الوارد الذي يجيب عنه هذا الكتاب (اختياري)</summary>
        public int? ReplyToId { get; set; }
    }

    public class UpdateCorrespondenceRequestDto
    {
        public int TypeId { get; set; }
        public string Party { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? ExternalNumber { get; set; }
        public DateTime? ExternalDate { get; set; }
        public int? ProjectId { get; set; }
        public int? ReplyToId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class ReferCorrespondenceRequestDto
    {
        public int ToDepartmentId { get; set; }
        public int? ToOfficeId { get; set; }
        public int? ToUserId { get; set; }
        public string? Note { get; set; }
    }

    public class ChangeCorrespondenceStatusRequestDto
    {
        public int StatusId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class DecideOutgoingDraftRequestDto
    {
        public bool Approve { get; set; }
        /// <summary>سبب الإعادة (مطلوب عند الرفض)</summary>
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
