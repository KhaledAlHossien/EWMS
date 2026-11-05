namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>
    /// التكليف الداخلي لبيان مشروع (IPA) — واحد لكل طلب (RequestId فريد). كود المشروع يولد هنا،
    /// ويُنسخ إلى المشروع عند إنشائه. يعتمده رئيس الفرع.
    /// </summary>
    public class StudyAssignment : IStudyDocument
    {
        public int Id { get; set; }

        public int RequestId { get; set; }
        public StudyRequest Request { get; set; } = null!;

        public int FollowingDepartmentId { get; set; }
        public Department FollowingDepartment { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public string AssignmentNumber { get; set; } = string.Empty;   // IPA-26-0001
        public DateTime AssignmentDate { get; set; }
        public string? ProjectCode { get; set; }
        public decimal? AllocatedBudget { get; set; }
        public string ScopeSummary { get; set; } = string.Empty;

        // المطلوب من القسم المتابع (مربعات النموذج الورقي)
        public bool NeedsTechnicalEvaluation { get; set; }
        public bool NeedsResourceEstimate { get; set; }
        public bool NeedsPreliminaryBudget { get; set; }
        public bool NeedsFinalRecommendation { get; set; }

        public DateTime ReferralDate { get; set; }
        public DateTime ResponseDeadline { get; set; }
        public StudyPriority Priority { get; set; }

        public StudyDocumentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        /// <summary>إشعار الرد (واحد على الأكثر، لا يُعاد)</summary>
        public StudyAssignmentResponse? Response { get; set; }
    }
}
