namespace Application.DTOs.Response
{
    public class DeliverableTypeResponseDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DocumentKind { get; set; }
        public string DocumentKindAr { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>سطر في قائمة الدراسات</summary>
    public class StudyListItemDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ExternalNumber { get; set; }
        public int Mode { get; set; }
        public string ModeAr { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusAr { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public int DeliverablesTotal { get; set; }
        public int DeliverablesApproved { get; set; }
        public int ApprovedPercent { get; set; }
        /// <summary>فيه مخرج تجاوز موعده (مرشّح متابعة، لا قرار)</summary>
        public bool IsOverdue { get; set; }
        public int? CorrespondenceId { get; set; }
        public string? CorrespondenceNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StudyDeliverableDto
    {
        public int Id { get; set; }
        public int TypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int? OfficeId { get; set; }
        public string? OfficeName { get; set; }
        public int? AssigneeUserId { get; set; }
        public string? AssigneeName { get; set; }
        public DateTime? DueDate { get; set; }
        public string Note { get; set; } = string.Empty;
        public int State { get; set; }
        public string StateAr { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }

        public int? DocumentId { get; set; }
        public int? LatestVersionId { get; set; }
        public int? LatestVersionNumber { get; set; }
        public int? SubmittedVersionId { get; set; }
        public int? SubmittedVersionNumber { get; set; }
        public string? SubmittedByName { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReturnNote { get; set; }
        public int? ApprovedVersionId { get; set; }
        public int? ApprovedVersionNumber { get; set; }

        /// <summary>المخرج الذي ينتظره هذا المخرج في التسلسل (اسمه)، وإلا null</summary>
        public string? BlockedByName { get; set; }
        public bool CanPrepare { get; set; }
        public bool CanAttachDocument { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanReview { get; set; }
        public bool CanSendBack { get; set; }
        public bool CanEdit { get; set; }
        public bool CanRemove { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class StudySubmissionDto
    {
        public int Id { get; set; }
        public int Round { get; set; }
        public DateTime SentAt { get; set; }
        public string SentByName { get; set; } = string.Empty;
        public int? OutgoingCorrespondenceId { get; set; }
        public string? OutgoingCorrespondenceNumber { get; set; }
        public string Note { get; set; } = string.Empty;
        public int Decision { get; set; }
        public string DecisionAr { get; set; } = string.Empty;
        public DateTime? DecidedAt { get; set; }
        public string? DecidedByName { get; set; }
        public string DecisionNote { get; set; } = string.Empty;
        public int? DecisionVersionId { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class StudyDetailDto : StudyListItemDto
    {
        public string Notes { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public List<StudyDeliverableDto> Deliverables { get; set; } = [];
        public List<StudySubmissionDto> Submissions { get; set; } = [];

        public bool CanEdit { get; set; }
        public bool CanAddDeliverable { get; set; }
        public bool CanSend { get; set; }
        public bool CanDecide { get; set; }
        public bool CanReopen { get; set; }
        public bool CanCancel { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
