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
    }
}
