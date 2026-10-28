namespace Application.DTOs.Response
{
    public class CorrespondenceTypeResponseDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? DeadlineDays { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public class CorrespondenceStatusResponseDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Stage { get; set; }
        public string StageAr { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>صف قائمة المعاملات (بلا إحالات ولا مرفقات)</summary>
    public class CorrespondenceListItemDto
    {
        public int Id { get; set; }
        /// <summary>IN-2026-00012 أو OUT-…؛ null لمسودة لم تصدر</summary>
        public string? Number { get; set; }
        public int Direction { get; set; }
        public string DirectionAr { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Party { get; set; } = string.Empty;
        public string? ExternalNumber { get; set; }
        public int TypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int? StatusId { get; set; }
        public string? StatusName { get; set; }
        public string? StatusColor { get; set; }
        public int? Stage { get; set; }
        public int? OutgoingState { get; set; }
        public string? OutgoingStateAr { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? CurrentUnitName { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsClosed { get; set; }
        public DateTime RegisteredAt { get; set; }
        public string RegisteredByName { get; set; } = string.Empty;
    }

    public class CorrespondenceReferralDto
    {
        public int Id { get; set; }
        public int FromUserId { get; set; }
        public string FromUserName { get; set; } = string.Empty;
        public string? FromDepartmentName { get; set; }
        public int ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; } = string.Empty;
        public int? ToOfficeId { get; set; }
        public string? ToOfficeName { get; set; }
        public int? ToUserId { get; set; }
        public string? ToUserName { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedByName { get; set; }
    }

    public class CorrespondenceAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    /// <summary>تفصيل معاملة مع إحالاتها ومرفقاتها وما يحق للمستخدم فعله عليها الآن (تحسبه الخدمة فلا تكرّر الواجهة القواعد)</summary>
    public class CorrespondenceResponseDto : CorrespondenceListItemDto
    {
        public string Summary { get; set; } = string.Empty;
        public DateTime? ExternalDate { get; set; }
        public int? ReplyToId { get; set; }
        public string? ReplyToNumber { get; set; }
        public int? CurrentDepartmentId { get; set; }
        public int? CurrentOfficeId { get; set; }
        public int? CurrentUserId { get; set; }
        public string? CurrentUserName { get; set; }

        public string? ReturnNote { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? IssuedAt { get; set; }
        public string? IssuedByName { get; set; }
        public DateTime? ClosedAt { get; set; }

        public List<CorrespondenceReferralDto> Referrals { get; set; } = [];
        public List<CorrespondenceAttachmentDto> Attachments { get; set; } = [];

        public bool CanEdit { get; set; }
        public bool CanRefer { get; set; }
        public bool CanAcknowledge { get; set; }
        public bool CanChangeStatus { get; set; }
        public bool CanClose { get; set; }
        public bool CanAttach { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanApprove { get; set; }
        public bool CanIssue { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class CorrespondenceHistoryDto
    {
        public long Id { get; set; }
        public DateTime At { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    public class ReferralOfficeDto { public int Id { get; set; } public string Name { get; set; } = string.Empty; }

    public class ReferralDepartmentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ReferralOfficeDto> Offices { get; set; } = [];
    }

    /// <summary>موظف يصلح هدف إحالة: يملك صلاحية المعالجة في الوحدة المختارة</summary>
    public class ReferralUserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? OfficeName { get; set; }
    }
}
