namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>
    /// نوع مرحلة (قائمة ديناميكية تديرها إدارة المشاريع). عند إنشاء مشروع تُنسخ الأنواع الفعّالة إلى مراحله
    /// بترتيبها ووزنها الافتراضيين؛ النوع المرتبط بمستند تُنشأ مرحلته مغلقة وتواريخها من ذلك المستند.
    /// </summary>
    public class StudyStageType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DefaultOrder { get; set; }
        public decimal DefaultWeight { get; set; }
        public StudyLinkedDocument LinkedDocument { get; set; }
        public bool IsActive { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>نوع مرفق (كتاب، مخطط، صورة موقع…)</summary>
    public class StudyAttachmentType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    /// <summary>اختصاص هندسي: مدني، معماري، ديكور، كهرباء، ميكانيك…</summary>
    public class EngineeringDiscipline
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
