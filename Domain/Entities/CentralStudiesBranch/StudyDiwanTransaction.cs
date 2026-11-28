namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>قرار الديوان المركزي في كتاب الرد</summary>
    public enum StudyDiwanDecision { Approved = 1, Rejected = 2 }

    /// <summary>
    /// معاملة الديوان (DWN) — مستند من مستندات الدراسة بين إشعار الرد المعتمد وأمر البدء (طلب الواجهة 2026-11-26، بدل
    /// StudyDiwanCase/StudyDiwanReply): واحدة لكل رد (ResponseId فريد). إنشاؤها = تسجيل الصادر إلى الديوان المركزي،
    /// ثم يُسجَّل الرد المركزي (رقمه وتاريخه وقراره) وتُرفق صورة كتابه في StudyAttachments، وتُرسل للاعتماد فيوقّعها رئيس الفرع.
    /// اعتمادها بقرار موافقة يتيح أمر البدء (ما لم تكن التوصية «غير متاح» فتُغلق السلسلة). القرار «رفض» يجعلها مرفوضة ويعيد الرد
    /// إلى القسم المتابع؛ بعد اعتماد الرد المعدّل تُعدَّل المعاملة نفسها بصادر ورد جديدين وتُرسل من جديد.
    /// </summary>
    public class StudyDiwanTransaction : IStudyDocument
    {
        public int Id { get; set; }

        public int ResponseId { get; set; }
        public StudyAssignmentResponse Response { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public string TransactionNumber { get; set; } = string.Empty;   // DWN-26-0001

        /// <summary>كتاب الصادر إلى الديوان المركزي</summary>
        public string DispatchNumber { get; set; } = string.Empty;
        public DateTime DispatchDate { get; set; }

        /// <summary>كتاب الرد المركزي (يُكمَل قبل الإرسال للاعتماد)</summary>
        public string? ReplyNumber { get; set; }
        public DateTime? ReplyDate { get; set; }
        public StudyDiwanDecision? CentralDecision { get; set; }

        public string? Notes { get; set; }

        public StudyDocumentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
