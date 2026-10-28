namespace Application.DTOs.Request
{
    public class DeliverableTypeRequestDto
    {
        /// <summary>فرع النوع: فرع المستخدم، ولنظرة المؤسسة أي فرع</summary>
        public int? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>نوع الوثيقة في الإضبارة (ProjectDocumentKind)</summary>
        public int DocumentKind { get; set; } = 99;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public class StudyOrderRequestDto
    {
        public int ProjectId { get; set; }
        /// <summary>المعاملة الواردة التي وصل بها الأمر (اختياري)</summary>
        public int? CorrespondenceId { get; set; }
        public string? ExternalNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        /// <summary>StudyMode: 1 بالتسلسل، 2 بالتوازي</summary>
        public int Mode { get; set; } = 1;
        public DateTime? DueDate { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class StudyDeliverableRequestDto
    {
        public int TypeId { get; set; }
        public int? OfficeId { get; set; }
        public int? AssigneeUserId { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Note { get; set; }
        /// <summary>موضعه في التسلسل (يوضع في الآخر إن لم يُحدَّد)</summary>
        public int? SortOrder { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class AttachDeliverableDocumentRequestDto
    {
        /// <summary>وثيقة من إضبارة المشروع نفسه رفعها المُعِدّ (تُنشأ بالرفع المجزّأ ثم تُربط هنا)</summary>
        public int DocumentId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class SubmitDeliverableRequestDto
    {
        public byte[]? RowVersion { get; set; }
    }

    public class ReviewDeliverableRequestDto
    {
        public bool Approve { get; set; }
        /// <summary>سبب الإعادة (مطلوب عند الإعادة)</summary>
        public string? Note { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class SendBackDeliverableRequestDto
    {
        public string Note { get; set; } = string.Empty;
        public byte[]? RowVersion { get; set; }
    }

    public class StudyStatusRequestDto
    {
        public byte[]? RowVersion { get; set; }
    }

    public class SendStudyRequestDto
    {
        public string? Note { get; set; }
        /// <summary>الكتاب الصادر الذي أُرسلت به الدراسة (اختياري)</summary>
        public int? OutgoingCorrespondenceId { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class DecideStudyRequestDto
    {
        /// <summary>CenterDecision: 2 اعتماد، 3 ملاحظات</summary>
        public int Decision { get; set; }
        public string? Note { get; set; }
        /// <summary>إصدار من وثائق المشروع هو قرار المركزية (اختياري)</summary>
        public int? DecisionVersionId { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
