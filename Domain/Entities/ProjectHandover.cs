namespace Domain.Entities
{
    /// <summary>نوع الاستلام الرسمي: أولي أو نهائي، كل منهما سجل بلجنته ونتيجته. القيم لا تُعاد استعمالها.</summary>
    public enum HandoverKind
    {
        Initial = 1,
        Final = 2
    }

    /// <summary>مسار محضر الاستلام: طلب ← لجنة مشكَّلة ← مُنتهٍ بنتيجة. (الإلغاء بديل الحذف)</summary>
    public enum HandoverStatus
    {
        Requested = 1,
        CommitteeFormed = 2,
        Concluded = 3,
        Cancelled = 4
    }

    /// <summary>نتيجة الاستلام: بلا ملاحظات، أو بملاحظات تُتابَع حتى تُغلق</summary>
    public enum HandoverResult
    {
        WithoutNotes = 1,
        WithNotes = 2
    }

    /// <summary>حالة ملاحظة اللجنة: مفتوحة ← عولجت ← تحقق منها غير من عالجها (مغلقة)</summary>
    public enum HandoverNoteStatus
    {
        Open = 1,
        Fixed = 2,
        Verified = 3
    }

    /// <summary>
    /// محضر استلام (2026-10-31، المرحلة 4): يبدأ بطلب مهندس الموقع (يمرّ عبر الديوان)، فتشكَّل لجنة استلام، فيُسجَّل تاريخ الكشف والنتيجة.
    /// أولي ونهائي سجلان منفصلان؛ طلب جديد بعد ملاحظات = سجل جديد. لا يُغلق المشروع آلياً لانتهاء التاريخ: الإغلاق بقرار على مرحلة المشروع.
    /// وثائقه (المحضر وتقرير الإشراف) إصدارات مثبَّتة من إضبارة المشروع.
    /// </summary>
    public class HandoverRecord
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public int? ContractId { get; set; }
        public Contract? Contract { get; set; }

        public HandoverKind Kind { get; set; }
        public HandoverStatus Status { get; set; } = HandoverStatus.Requested;

        public int RequestedByUserId { get; set; }
        public User RequestedByUser { get; set; } = null!;
        public DateTime RequestedAt { get; set; }
        /// <summary>معاملة الديوان التي مرّ بها طلب الاستلام (اختياري)</summary>
        public int? CorrespondenceId { get; set; }
        public Correspondence? Correspondence { get; set; }
        public string RequestNote { get; set; } = string.Empty;

        public DateTime? InspectionDate { get; set; }
        public HandoverResult? Result { get; set; }
        public string ResultNote { get; set; } = string.Empty;
        public int? MinutesVersionId { get; set; }
        public ProjectDocumentVersion? MinutesVersion { get; set; }
        public int? SupervisionReportVersionId { get; set; }
        public ProjectDocumentVersion? SupervisionReportVersion { get; set; }
        public int? ConcludedByUserId { get; set; }
        public User? ConcludedByUser { get; set; }
        public DateTime? ConcludedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Committee? Committee { get; set; }
        public ICollection<HandoverNote> Notes { get; set; } = new List<HandoverNote>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>
    /// ملاحظة لجنة الاستلام على عمل يحتاج معالجة: تُسند إلى مسؤول (موظف أو جهة بالاسم) وتُتابَع حتى تُغلق. من عالجها لا يتحقق منها.
    /// </summary>
    public class HandoverNote
    {
        public int Id { get; set; }
        public int HandoverRecordId { get; set; }
        public HandoverRecord HandoverRecord { get; set; } = null!;

        public string Description { get; set; } = string.Empty;
        public int? ResponsibleUserId { get; set; }
        public User? ResponsibleUser { get; set; }
        /// <summary>الجهة المسؤولة بالاسم عندما لا تكون موظفاً (المقاول مثلاً)</summary>
        public string? ResponsibleName { get; set; }
        public DateTime? DueDate { get; set; }

        public HandoverNoteStatus Status { get; set; } = HandoverNoteStatus.Open;
        public DateTime? FixedAt { get; set; }
        public int? FixedByUserId { get; set; }
        public User? FixedByUser { get; set; }
        public string FixNote { get; set; } = string.Empty;
        public DateTime? VerifiedAt { get; set; }
        public int? VerifiedByUserId { get; set; }
        public User? VerifiedByUser { get; set; }
        /// <summary>وثيقة المعالجة أو التحقق (إصدار مثبَّت، اختياري)</summary>
        public int? EvidenceVersionId { get; set; }
        public ProjectDocumentVersion? EvidenceVersion { get; set; }

        /// <summary>موعد المعالجة الذي نُبّه عن تجاوزه (مرة لكل موعد؛ تغيير الموعد يسمح بتنبيه جديد)</summary>
        public DateTime? OverdueNotifiedFor { get; set; }

        public DateTime CreatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
