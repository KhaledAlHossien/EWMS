namespace Application.Common
{
    /// <summary>مفاتيح الأفعال في سجل التدقيق (نصوص ثابتة تُخزَّن، فلا تُغيَّر بعد استعمالها)</summary>
    public static class AuditActions
    {
        public const string Created = "created";
        public const string Updated = "updated";
        public const string Stage = "stage";
        public const string MemberAdded = "member-added";
        public const string MemberRemoved = "member-removed";
        public const string Uploaded = "uploaded";
        public const string Approved = "approved";
        public const string Downloaded = "downloaded";
        public const string Deleted = "deleted";
        public const string Progress = "progress";
        public const string Classified = "classified";
        public const string Reordered = "reordered";
        public const string Justified = "justified";
        public const string Submitted = "submitted";
        public const string Rejected = "rejected";
        public const string Referred = "referred";
        public const string Acknowledged = "acknowledged";
        public const string Closed = "closed";
        public const string Reopened = "reopened";
        public const string Returned = "returned";
        public const string Issued = "issued";
        public const string Attached = "attached";
        public const string Sent = "sent";
        public const string Decided = "decided";
        public const string Cancelled = "cancelled";
        public const string Awarded = "awarded";
        public const string Recorded = "recorded";
    }

    /// <summary>أنواع السجلات في سجل التدقيق (وهي نفسها RelatedEntityType لإشعارات المشروع)</summary>
    public static class AuditEntities
    {
        public const string Project = "Project";
        public const string Document = "ProjectDocument";
        public const string Version = "ProjectDocumentVersion";
        public const string StartOrder = "ProjectStartOrder";
        public const string Schedule = "ProjectSchedule";
        public const string ScheduleTask = "ScheduleTask";
        public const string ScheduleDependency = "ScheduleDependency";
        public const string Delay = "ScheduleDelay";
        public const string Extension = "ScheduleExtension";
        public const string Correspondence = "Correspondence";
        public const string CorrespondenceType = "CorrespondenceType";
        public const string CorrespondenceStatus = "CorrespondenceStatus";
        public const string Study = "StudyOrder";
        public const string Deliverable = "StudyDeliverable";
        public const string DeliverableType = "DeliverableType";
        public const string Submission = "StudySubmission";
        public const string Contract = "Contract";
        public const string Contractor = "Contractor";
        public const string EvaluationCriterion = "EvaluationCriterion";
        public const string ContractEvaluation = "ContractorEvaluation";
        public const string Handover = "HandoverRecord";
    }
}
