namespace Application.DTOs.Request
{
    /// <summary>
    /// إنشاء مشروع أو تعديله. الفرع لا يتغير بعد الإنشاء، والمرحلة تُهمل عند الإنشاء (يبدأ دراسة).
    /// RowVersion يُعاد كما وصل في الاستجابة؛ إن تغيّر المشروع منذ فتحه يُرفض الحفظ برسالة واضحة.
    /// </summary>
    public class ProjectRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? Beneficiary { get; set; }
        public string? Description { get; set; }
        /// <summary>عند الإنشاء فقط، ولنظرة المؤسسة وحدها (غيرهم يُنشئون في فرعهم)</summary>
        public int? BranchId { get; set; }
        /// <summary>قسم المعالجة؛ فارغ عند الإنشاء = قسم المنشئ</summary>
        public int? DepartmentId { get; set; }
        public int? SiteId { get; set; }
        /// <summary>عند التعديل فقط (ProjectStage)</summary>
        public int? Stage { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class AddProjectMemberRequestDto
    {
        public int UserId { get; set; }
        public string? Title { get; set; }
    }
}
